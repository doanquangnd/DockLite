using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DockLite.Contracts.Api;
using DockLite.Core;
using DockLite.Core.Configuration;
using DockLite.Core.Diagnostics;
using DockLite.Infrastructure.Api;

namespace DockLite.Infrastructure.Wsl;

/// <summary>
/// Nếu service chưa phản hồi, gọi wsl.exe chạy bash scripts/restart-server.sh trong thư mục wsl-docker-service (không dùng ./ để tránh Permission denied).
/// </summary>
public static class WslDockerServiceAutoStart
{
    private static readonly JsonSerializerOptions DockerInfoJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private const int MaxRecentWslLines = 48;

    /// <summary>
    /// Dòng stdout/stderr gần nhất (sau lần spawn WSL cuối) để gợi ý khi health timeout.
    /// </summary>
    private static readonly object WslRecentOutputLock = new();

    private static readonly List<string> RecentWslLines = new();

    private static string? _lastWslLaunchInfo;
    private static string? _lastWslCommandSummary;

    /// <summary>
    /// Giữ tham chiếu Process để tiếp tục nhận sự kiện stdout/stderr (tránh GC làm mất đọc bất đồng bộ).
    /// </summary>
    private static readonly object WslProcessLock = new();

    private static readonly List<Process> WslRunServerProcesses = new();

    /// <summary>Giây chờ health sau khi spawn WSL khi mở app (theo cài đặt, mặc định 30).</summary>
    public static int GetHealthWaitAfterWslSeconds(AppSettings settings)
    {
        int v = settings.WslAutoStartHealthWaitSeconds;
        return v is >= 10 and <= 600 ? v : 30;
    }

    private static int GetManualHealthWaitSeconds(AppSettings settings)
    {
        int v = settings.WslManualHealthWaitSeconds;
        return v is >= 10 and <= 600 ? v : 90;
    }

    private static TimeSpan GetSingleHealthProbeTimeout(AppSettings settings)
    {
        int v = settings.HealthProbeSingleRequestSeconds;
        return TimeSpan.FromSeconds(v is >= 1 and <= 60 ? v : 3);
    }

    private static TimeSpan GetHealthPollInterval(AppSettings settings)
    {
        int ms = settings.WslHealthPollIntervalMilliseconds;
        ms = ms is >= 100 and <= 5000 ? ms : 500;
        return TimeSpan.FromMilliseconds(ms);
    }

    /// <summary>
    /// Kiểm tra GET api/health; nếu lỗi và bật tự khởi động thì chạy WSL (restart-server.sh) và chờ service sẵn sàng.
    /// </summary>
    /// <returns>Ok nếu cuối cùng health OK; Reason mô tả khi không đạt.</returns>
    public static async Task<(bool Ok, WslEnsureFailureReason Reason)> TryEnsureRunningAsync(
        DockLiteHttpSession httpSession,
        AppSettings settings,
        string appBaseDirectory,
        CancellationToken cancellationToken = default,
        IProgress<WslStartupProgress>? progress = null)
    {
        if (!settings.AutoStartWslService)
        {
            progress?.Report(new WslStartupProgress(WslStartupPhase.CheckingInitialHealth, null));
            bool healthOk = await IsHealthOkWithRetryAsync(httpSession, settings, cancellationToken).ConfigureAwait(false);
            if (!healthOk)
            {
                TelemetryEnsureStartup(settings, "startup_health_fail_autostart_off", ("reason", nameof(WslEnsureFailureReason.HealthUnavailableWhenAutoStartOff)));
            }

            return (
                healthOk,
                healthOk ? WslEnsureFailureReason.None : WslEnsureFailureReason.HealthUnavailableWhenAutoStartOff);
        }

        progress?.Report(new WslStartupProgress(WslStartupPhase.CheckingInitialHealth, null));
        if (await IsHealthOkWithRetryAsync(httpSession, settings, cancellationToken).ConfigureAwait(false))
        {
            TelemetryEnsureStartup(settings, "startup_health_ok_initial");
            return (true, WslEnsureFailureReason.None);
        }

        return await TrySpawnWslRestartAndWaitForHealthAsync(
            httpSession,
            settings,
            appBaseDirectory,
            cancellationToken,
            progress,
            telemetryEventSuffix: "startup_wsl_restart_spawned").ConfigureAwait(false);
    }

    /// <summary>
    /// Gọi <c>scripts/restart-server.sh</c> trong WSL rồi chờ GET /api/health (không probe trước khi spawn).
    /// Dùng khi khôi phục sau mở app: probe đầu có thể đủ để bỏ qua restart nhưng kết nối thực tế vẫn lỗi (WSL/TCP).
    /// </summary>
    public static Task<(bool Ok, WslEnsureFailureReason Reason)> TrySpawnWslRestartAndWaitForHealthAsync(
        DockLiteHttpSession httpSession,
        AppSettings settings,
        string appBaseDirectory,
        CancellationToken cancellationToken,
        IProgress<WslStartupProgress>? progress = null) =>
        TrySpawnWslRestartAndWaitForHealthAsync(
            httpSession,
            settings,
            appBaseDirectory,
            cancellationToken,
            progress,
            telemetryEventSuffix: "startup_wsl_restart_recovery_spawned");

    private static async Task<(bool Ok, WslEnsureFailureReason Reason)> TrySpawnWslRestartAndWaitForHealthAsync(
        DockLiteHttpSession httpSession,
        AppSettings settings,
        string appBaseDirectory,
        CancellationToken cancellationToken,
        IProgress<WslStartupProgress>? progress,
        string telemetryEventSuffix)
    {
        string? root = ResolveWindowsRoot(settings, appBaseDirectory);
        if (string.IsNullOrEmpty(root))
        {
            Debug.WriteLine("DockLite: không xác định được thư mục wsl-docker-service (cấu hình hoặc tìm tự động).");
            TelemetryEnsureStartup(settings, "startup_fail_missing_service_root", ("reason", nameof(WslEnsureFailureReason.MissingServiceRoot)));
            return (false, WslEnsureFailureReason.MissingServiceRoot);
        }

        string restartScript = Path.Combine(root, "scripts", "restart-server.sh");
        if (!File.Exists(restartScript))
        {
            Debug.WriteLine("DockLite: thiếu scripts/restart-server.sh tại " + root);
            TelemetryEnsureStartup(settings, "startup_fail_missing_restart_script", ("reason", nameof(WslEnsureFailureReason.MissingRestartScript)));
            return (false, WslEnsureFailureReason.MissingRestartScript);
        }

        string? distro = ResolveEffectiveDistribution(settings);
        if (!TryGetWslUnixPath(root, distro, out string wslPath, out string? _))
        {
            Debug.WriteLine("DockLite: wslpath thất bại cho " + root);
            TelemetryEnsureStartup(settings, "startup_fail_wslpath", ("reason", nameof(WslEnsureFailureReason.WslPathConversionFailed)));
            return (false, WslEnsureFailureReason.WslPathConversionFailed);
        }

        progress?.Report(new WslStartupProgress(WslStartupPhase.LaunchingWslScript, null));
        TelemetryEnsureStartup(settings, telemetryEventSuffix, ("wait_sec_max", GetHealthWaitAfterWslSeconds(settings).ToString()));
        SpawnWslLifecycleScript(wslPath, distro, "scripts/restart-server.sh");

        TimeSpan waitTotal = TimeSpan.FromSeconds(GetHealthWaitAfterWslSeconds(settings));
        DateTime deadline = DateTime.UtcNow + waitTotal;
        TimeSpan pollInterval = GetHealthPollInterval(settings);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int remaining = Math.Max(0, (int)Math.Ceiling((deadline - DateTime.UtcNow).TotalSeconds));
            progress?.Report(new WslStartupProgress(WslStartupPhase.WaitingHealthAfterWsl, remaining));
            await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            if (await IsConnectivityStableOkAsync(httpSession, settings, cancellationToken).ConfigureAwait(false))
            {
                TelemetryEnsureStartup(settings, "startup_health_ok_after_wsl_restart");
                return (true, WslEnsureFailureReason.None);
            }

            double ms = Math.Min(5000.0, pollInterval.TotalMilliseconds * 1.5);
            pollInterval = TimeSpan.FromMilliseconds(ms);
        }

        string hint = FormatHealthTimeoutUserHint(AppFileLog.LogDirectory);
        AppFileLog.WriteMultiline("WSL health timeout", hint);
        TelemetryEnsureStartup(
            settings,
            "startup_health_timeout_after_wsl_restart",
            ("reason", nameof(WslEnsureFailureReason.HealthTimeoutAfterWslStart)),
            ("wait_sec_max", GetHealthWaitAfterWslSeconds(settings).ToString()));
        return (false, WslEnsureFailureReason.HealthTimeoutAfterWslStart);
    }

    private static void TelemetryEnsureStartup(AppSettings settings, string eventId, params (string key, string value)[] extra)
    {
        if (!DiagnosticTelemetry.IsEnabled)
        {
            return;
        }

        var list = new List<(string key, string value)>
        {
            ("base_url", DiagnosticTelemetry.FormatBaseUrlForTelemetry(settings.ServiceBaseUrl)),
        };
        list.AddRange(extra);
        DiagnosticTelemetry.WriteEvent(eventId, list.ToArray());
    }

    /// <summary>
    /// Gợi ý hiển thị cho người dùng khi đã spawn WSL nhưng /api/health không kịp (lệnh, distro/path, vài dòng output gần nhất).
    /// </summary>
    /// <param name="includeLeadSummary">false khi đã có đoạn mở đầu riêng (ví dụ chờ health thủ công 90 giây).</param>
    public static string FormatHealthTimeoutUserHint(string logDirectory, bool includeLeadSummary = true)
    {
        lock (WslRecentOutputLock)
        {
            var sb = new StringBuilder();
            if (includeLeadSummary)
            {
                sb.AppendLine("Không nhận /api/health trong thời gian chờ sau khi gọi WSL (restart service).");
                sb.AppendLine();
            }
            if (!string.IsNullOrEmpty(_lastWslCommandSummary))
            {
                sb.AppendLine("Lệnh (tham khảo):");
                sb.AppendLine(_lastWslCommandSummary);
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(_lastWslLaunchInfo))
            {
                sb.AppendLine(_lastWslLaunchInfo);
                sb.AppendLine();
            }

            if (RecentWslLines.Count > 0)
            {
                sb.AppendLine("Output gần đây (stdout/stderr):");
                foreach (string ln in RecentWslLines)
                {
                    sb.AppendLine(ln);
                }

                sb.AppendLine();
            }
            else
            {
                sb.AppendLine(
                    "(Chưa thu được dòng stdout/stderr nào — tiến trình có thể chưa kịp in hoặc lỗi trước khi spawn.)");
                sb.AppendLine();
            }

            sb.Append("Thư mục log ứng dụng: ");
            sb.Append(logDirectory);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Gọi wsl.exe chạy bash scripts/run-server.sh (không chờ health). Dùng khi người dùng khởi động thủ công từ Cài đặt.
    /// </summary>
    /// <returns>true nếu đã gửi lệnh tới wsl.exe; false nếu thiếu đường dẫn hoặc lỗi.</returns>
    public static bool TryStartServiceManually(AppSettings settings, string appBaseDirectory, out string userMessage)
    {
        userMessage = "";
        string? root = ResolveWindowsRoot(settings, appBaseDirectory);
        if (string.IsNullOrEmpty(root))
        {
            userMessage =
                "Không tìm thấy thư mục wsl-docker-service. Điền đường dẫn Windows hoặc đặt exe cùng cây thư mục với clone.";
            return false;
        }

        string runScript = Path.Combine(root, "scripts", "run-server.sh");
        if (!File.Exists(runScript))
        {
            userMessage = "Không thấy scripts/run-server.sh tại: " + root;
            return false;
        }

        string? distro = ResolveEffectiveDistribution(settings);
        if (!TryGetWslUnixPath(root, distro, out string wslPath, out string? wslpathErr))
        {
            userMessage = FormatWslpathUserMessage(
                "Không chuyển được đường dẫn sang Unix (wslpath).",
                wslpathErr);
            return false;
        }

        try
        {
            SpawnWslLifecycleScript(wslPath, distro, "scripts/run-server.sh");
        }
        catch (Exception ex)
        {
            userMessage = "Không chạy được wsl.exe: " + ex.Message;
            return false;
        }

        userMessage =
            "Đã gửi lệnh khởi động service trong WSL (bash scripts/run-server.sh). Đợi build hoặc vài giây rồi nhấn Kiểm tra kết nối.";
        return true;
    }

    /// <summary>
    /// Gọi wsl.exe chạy bash scripts/stop-server.sh (dừng tiến trình docklite-wsl).
    /// </summary>
    public static bool TryStopServiceManually(AppSettings settings, string appBaseDirectory, out string userMessage)
    {
        userMessage = "";
        string? root = ResolveWindowsRoot(settings, appBaseDirectory);
        if (string.IsNullOrEmpty(root))
        {
            userMessage =
                "Không tìm thấy thư mục wsl-docker-service. Điền đường dẫn Windows hoặc đặt exe cùng cây thư mục với clone.";
            return false;
        }

        string stopScript = Path.Combine(root, "scripts", "stop-server.sh");
        if (!File.Exists(stopScript))
        {
            userMessage = "Không thấy scripts/stop-server.sh tại: " + root;
            return false;
        }

        string? distro = ResolveEffectiveDistribution(settings);
        if (!TryGetWslUnixPath(root, distro, out string wslPath, out string? wslpathErr))
        {
            userMessage = FormatWslpathUserMessage(
                "Không chuyển được đường dẫn sang Unix (wslpath).",
                wslpathErr);
            return false;
        }

        try
        {
            SpawnWslLifecycleScript(wslPath, distro, "scripts/stop-server.sh");
        }
        catch (Exception ex)
        {
            userMessage = "Không chạy được wsl.exe: " + ex.Message;
            return false;
        }

        userMessage = "Đã gửi lệnh dừng service trong WSL (bash scripts/stop-server.sh).";
        return true;
    }

    /// <summary>
    /// Gọi wsl.exe chạy bash scripts/build-server.sh (go mod tidy + go build).
    /// </summary>
    public static bool TryBuildServiceManually(AppSettings settings, string appBaseDirectory, out string userMessage)
    {
        userMessage = "";
        string? root = ResolveWindowsRoot(settings, appBaseDirectory);
        if (string.IsNullOrEmpty(root))
        {
            userMessage =
                "Không tìm thấy thư mục wsl-docker-service. Điền đường dẫn Windows hoặc đặt exe cùng cây thư mục với clone.";
            return false;
        }

        string buildScript = Path.Combine(root, "scripts", "build-server.sh");
        if (!File.Exists(buildScript))
        {
            userMessage = "Không thấy scripts/build-server.sh tại: " + root;
            return false;
        }

        string? distro = ResolveEffectiveDistribution(settings);
        if (!TryGetWslUnixPath(root, distro, out string wslPath, out string? wslpathErr))
        {
            userMessage = FormatWslpathUserMessage(
                "Không chuyển được đường dẫn sang Unix (wslpath).",
                wslpathErr);
            return false;
        }

        try
        {
            SpawnWslLifecycleScript(wslPath, distro, "scripts/build-server.sh");
        }
        catch (Exception ex)
        {
            userMessage = "Không chạy được wsl.exe: " + ex.Message;
            return false;
        }

        userMessage =
            "Đã gửi lệnh build trong WSL (bash scripts/build-server.sh). Output ghi trong nhật ký ứng dụng; mở terminal WSL nếu cần xem trực tiếp.";
        return true;
    }

    /// <summary>
    /// Đồng bộ thư mục mã nguồn từ Windows (wslpath) sang đường dẫn Unix trong WSL (rsync nếu có, không thì cp -a).
    /// </summary>
    public static async Task<(bool Ok, string Message)> TrySyncWindowsSourceToLinuxDestinationAsync(
        AppSettings settings,
        string appBaseDirectory,
        CancellationToken cancellationToken = default)
    {
        string? root = ResolveWindowsSyncSourceRoot(settings, appBaseDirectory);
        if (string.IsNullOrEmpty(root))
        {
            bool hadExplicitSource = !string.IsNullOrWhiteSpace(settings.WslDockerServiceSyncSourceWindowsPath);
            return (
                false,
                hadExplicitSource
                    ? "Không tìm thấy thư mục nguồn đồng bộ (Windows). Kiểm tra ô «Nguồn trong Windows»."
                    : "Không tìm thấy thư mục wsl-docker-service. Điền đường dẫn dịch vụ hoặc nguồn đồng bộ, hoặc đặt exe cùng cây thư mục với clone.");
        }

        string? dstRaw = settings.WslDockerServiceLinuxSyncPath?.Trim();
        if (string.IsNullOrEmpty(dstRaw))
        {
            return (false, "Điền đường dẫn đích trong WSL (Unix, ví dụ /home/user/wsl-docker-service).");
        }

        if (!ValidateLinuxSyncDestination(dstRaw, out string? validationError))
        {
            return (false, validationError ?? "Đường dẫn đích không hợp lệ.");
        }

        string? distro = ResolveEffectiveDistribution(settings);
        if (!TryGetWslUnixPath(root, distro, out string srcUnix, out string? wslpathErr))
        {
            return (
                false,
                FormatWslpathUserMessage(
                    "Không chuyển được đường dẫn nguồn sang Unix (wslpath).",
                    wslpathErr));
        }

        string srcN = NormalizeUnixDirPath(srcUnix);
        string dstN = NormalizeUnixDirPath(dstRaw);
        if (string.Equals(srcN, dstN, StringComparison.Ordinal))
        {
            return (true, "Nguồn và đích trùng đường dẫn Unix — không cần đồng bộ.");
        }

        if (settings.WslDockerServiceSyncEnforceVersionGe)
        {
            if (!DockLiteSourceVersion.TryReadFromWindowsDirectory(root, out Version? srcVer, out string? srcErr))
            {
                return (false, srcErr ?? "Không đọc được version nguồn.");
            }

            (bool verOk, Version destVer, string? verErr) = await TryReadWslDestinationVersionForSyncAsync(dstN, distro, cancellationToken)
                .ConfigureAwait(false);
            if (!verOk)
            {
                return (false, verErr ?? "Không đọc được version đích.");
            }

            if (srcVer!.CompareTo(destVer) < 0)
            {
                return (
                    false,
                    "Version nguồn (" + srcVer + ") nhỏ hơn version trên đích (" + destVer
                    + "). Nâng file " + DockLiteSourceVersion.VersionFileName
                    + " trên Windows, hoặc tắt tùy chọn chỉ đồng bộ khi version nguồn >= đích.");
            }

            AppFileLog.Write(
                "WSL đồng bộ mã",
                "Kiểm tra version: nguồn=" + srcVer + " đích=" + destVer);
        }

        bool deleteExtra = settings.WslDockerServiceSyncDeleteExtra;
        string inner = BuildLinuxSyncBashScript(srcN, dstN, deleteExtra);

        using var p = new Process();
        p.StartInfo.FileName = "wsl.exe";
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        if (!string.IsNullOrWhiteSpace(distro))
        {
            p.StartInfo.ArgumentList.Add("-d");
            p.StartInfo.ArgumentList.Add(distro.Trim());
        }

        p.StartInfo.ArgumentList.Add("bash");
        p.StartInfo.ArgumentList.Add("-lc");
        p.StartInfo.ArgumentList.Add(inner);

        AppFileLog.Write(
            "WSL đồng bộ mã",
            "src=" + srcN + " dst=" + dstN + " deleteExtra=" + deleteExtra);

        try
        {
            p.Start();
            string stdout = await p.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            string stderr = await p.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await p.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (p.ExitCode != 0)
            {
                string err = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                if (string.IsNullOrWhiteSpace(err))
                {
                    err = "Mã thoát " + p.ExitCode;
                }

                return (false, "Đồng bộ thất bại: " + err.Trim());
            }

            string tail = string.IsNullOrWhiteSpace(stdout) ? "" : " " + stdout.Trim();
            return (
                true,
                "Đã đồng bộ mã nguồn tới " + dstN + "." + tail);
        }
        catch (Exception ex)
        {
            return (false, "Không chạy được wsl.exe: " + ex.Message);
        }
    }

    private static string NormalizeUnixDirPath(string path)
    {
        string t = path.Trim().Replace('\\', '/');
        while (t.Length > 1 && t.EndsWith('/'))
        {
            t = t[..^1];
        }

        return t;
    }

    private static bool ValidateLinuxSyncDestination(string path, out string? error)
    {
        error = null;
        string t = path.Trim();
        if (t.Length < 2 || !t.StartsWith('/'))
        {
            error = "Đường dẫn đích phải là Unix tuyệt đối (bắt đầu bằng /).";
            return false;
        }

        if (t.Contains("..", StringComparison.Ordinal))
        {
            error = "Đường dẫn đích không được chứa ..";
            return false;
        }

        foreach (char c in new[] { ';', '|', '&', '`', '$', '\r', '\n', '\t' })
        {
            if (t.Contains(c))
            {
                error = "Đường dẫn đích chứa ký tự không được phép.";
                return false;
            }
        }

        if (t == "/" || t == "/bin" || t == "/boot" || t == "/dev" || t == "/etc" || t == "/lib" || t == "/proc" || t == "/sys" || t == "/usr")
        {
            error = "Chọn thư mục con an toàn (không dùng thư mục hệ thống gốc).";
            return false;
        }

        return true;
    }

    private static string BashSingleQuoted(string s)
    {
        return "'" + s.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }

    /// <summary>
    /// Đọc file VERSION trong thư mục đích trên WSL; không có file → coi như 0.0.0.
    /// </summary>
    private static async Task<(bool Ok, Version DestVersion, string? Error)> TryReadWslDestinationVersionForSyncAsync(
        string dstDirUnix,
        string? distro,
        CancellationToken cancellationToken)
    {
        string baseDir = NormalizeUnixDirPath(dstDirUnix);
        string verNested = baseDir + "/internal/appversion/" + DockLiteSourceVersion.VersionFileName;
        string verRoot = baseDir + "/" + DockLiteSourceVersion.VersionFileName;
        string inner = "if test -f " + BashSingleQuoted(verNested) + "; then cat " + BashSingleQuoted(verNested)
            + "; elif test -f " + BashSingleQuoted(verRoot) + "; then cat " + BashSingleQuoted(verRoot) + "; fi";

        using var p = new Process();
        p.StartInfo.FileName = "wsl.exe";
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        if (!string.IsNullOrWhiteSpace(distro))
        {
            p.StartInfo.ArgumentList.Add("-d");
            p.StartInfo.ArgumentList.Add(distro.Trim());
        }

        p.StartInfo.ArgumentList.Add("bash");
        p.StartInfo.ArgumentList.Add("-lc");
        p.StartInfo.ArgumentList.Add(inner);

        try
        {
            p.Start();
            string stdout = await p.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await p.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await p.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (p.ExitCode != 0)
            {
                return (false, new Version(0, 0), "Không đọc được file VERSION trên đích (wsl, mã " + p.ExitCode + ").");
            }

            string t = stdout.Trim();
            if (string.IsNullOrEmpty(t))
            {
                return (true, new Version(0, 0), null);
            }

            if (!DockLiteSourceVersion.TryParseVersionLine(t, out Version? v, out string? err))
            {
                return (false, new Version(0, 0), "File VERSION trên đích không hợp lệ: " + err);
            }

            return (true, v!, null);
        }
        catch (Exception ex)
        {
            return (false, new Version(0, 0), "Lỗi khi đọc VERSION trên đích: " + ex.Message);
        }
    }

    private static string BuildLinuxSyncBashScript(string srcDir, string dstDir, bool deleteExtra)
    {
        string s = BashSingleQuoted(srcDir);
        string d = BashSingleQuoted(dstDir);
        string deleteFlag = deleteExtra ? "--delete " : "";
        return "set -eu; "
            + "mkdir -p " + d + "; "
            + "if command -v rsync >/dev/null 2>&1; then "
            + "rsync -a " + deleteFlag + s + "/ " + d + "/; "
            + "else "
            + "mkdir -p " + d + "; "
            + "cp -a " + s + "/. " + d + "/; "
            + "fi; "
            + "echo OK";
    }

    /// <summary>
    /// Gọi wsl.exe chạy bash scripts/restart-server.sh (pkill rồi run-server).
    /// </summary>
    public static bool TryRestartServiceManually(AppSettings settings, string appBaseDirectory, out string userMessage)
    {
        userMessage = "";
        string? root = ResolveWindowsRoot(settings, appBaseDirectory);
        if (string.IsNullOrEmpty(root))
        {
            userMessage =
                "Không tìm thấy thư mục wsl-docker-service. Điền đường dẫn Windows hoặc đặt exe cùng cây thư mục với clone.";
            return false;
        }

        string restartScript = Path.Combine(root, "scripts", "restart-server.sh");
        if (!File.Exists(restartScript))
        {
            userMessage = "Không thấy scripts/restart-server.sh tại: " + root;
            return false;
        }

        string? distro = ResolveEffectiveDistribution(settings);
        if (!TryGetWslUnixPath(root, distro, out string wslPath, out string? wslpathErr))
        {
            userMessage = FormatWslpathUserMessage(
                "Không chuyển được đường dẫn sang Unix (wslpath).",
                wslpathErr);
            return false;
        }

        try
        {
            SpawnWslLifecycleScript(wslPath, distro, "scripts/restart-server.sh");
        }
        catch (Exception ex)
        {
            userMessage = "Không chạy được wsl.exe: " + ex.Message;
            return false;
        }

        userMessage =
            "Đã gửi lệnh restart service trong WSL (bash scripts/restart-server.sh). Đợi vài giây rồi kiểm tra kết nối.";
        return true;
    }

    /// <summary>
    /// Restart trong WSL rồi chờ GET /api/health thành công (tối đa theo <see cref="AppSettings.WslManualHealthWaitSeconds"/>).
    /// </summary>
    public static async Task<(bool CommandSent, bool HealthOk, string Message)> TryRestartServiceManuallyAndWaitForHealthAsync(
        DockLiteHttpSession httpSession,
        AppSettings settings,
        string appBaseDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!TryRestartServiceManually(settings, appBaseDirectory, out string msg))
        {
            return (false, false, msg);
        }

        int manualSec = GetManualHealthWaitSeconds(settings);
        TimeSpan poll = GetHealthPollInterval(settings);
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(manualSec);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(poll, cancellationToken).ConfigureAwait(false);
            if (await IsConnectivityStableOkAsync(httpSession, settings, cancellationToken).ConfigureAwait(false))
            {
                return (
                    true,
                    true,
                    "Service đã phản hồi /api/health sau restart. Có thể dùng các chức năng hoặc nhấn Kiểm tra kết nối.");
            }
        }

        string hintForLog = FormatHealthTimeoutUserHint(AppFileLog.LogDirectory, includeLeadSummary: true);
        AppFileLog.WriteMultiline("WSL health timeout (restart)", hintForLog);
        string hintUi = FormatHealthTimeoutUserHint(AppFileLog.LogDirectory, includeLeadSummary: false);
        return (
            true,
            false,
            "Đã gửi restart tới WSL nhưng sau khoảng " + manualSec + " giây vẫn không kết nối được health. "
                + "Trong WSL xem log hoặc chạy tay bash scripts/restart-server.sh."
                + Environment.NewLine
                + Environment.NewLine
                + hintUi);
    }

    /// <summary>
    /// Gửi lệnh khởi động WSL rồi chờ GET /api/health thành công (tối đa theo <see cref="AppSettings.WslManualHealthWaitSeconds"/>).
    /// </summary>
    public static async Task<(bool CommandSent, bool HealthOk, string Message)> TryStartServiceManuallyAndWaitForHealthAsync(
        DockLiteHttpSession httpSession,
        AppSettings settings,
        string appBaseDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!TryStartServiceManually(settings, appBaseDirectory, out string msg))
        {
            return (false, false, msg);
        }

        int manualSec = GetManualHealthWaitSeconds(settings);
        TimeSpan poll = GetHealthPollInterval(settings);
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(manualSec);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(poll, cancellationToken).ConfigureAwait(false);
            if (await IsConnectivityStableOkAsync(httpSession, settings, cancellationToken).ConfigureAwait(false))
            {
                return (
                    true,
                    true,
                    "Service đã phản hồi /api/health. Có thể dùng các chức năng hoặc nhấn Kiểm tra kết nối để xem Docker.");
            }
        }

        string hintForLog = FormatHealthTimeoutUserHint(AppFileLog.LogDirectory, includeLeadSummary: true);
        AppFileLog.WriteMultiline("WSL health timeout", hintForLog);
        string hintUi = FormatHealthTimeoutUserHint(AppFileLog.LogDirectory, includeLeadSummary: false);
        return (
            true,
            false,
            "Đã gửi lệnh tới WSL nhưng sau khoảng " + manualSec + " giây vẫn không kết nối được health. "
                + "Trong WSL chạy tay: bash scripts/run-server.sh và xem có lỗi go build hay không; chạy go version nếu nghi PATH. "
                + "Đảm bảo Địa chỉ base URL trỏ đúng máy (127.0.0.1 hoặc IP WSL) và đã nhấn Lưu nếu vừa sửa ô địa chỉ."
                + Environment.NewLine
                + Environment.NewLine
                + hintUi);
    }

    private static string? ResolveWindowsRoot(AppSettings settings, string appBaseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(settings.WslDockerServiceWindowsPath))
        {
            string raw = settings.WslDockerServiceWindowsPath.Trim();
            // Đường dẫn \\wsl$\... hoặc \\wsl.localhost\... đôi khi Directory.Exists trả false dù Explorer mở được — vẫn thử wslpath.
            // Không trả về raw có dấu /: Path.GetFullPath + wslpath sẽ sai (mất dấu \ giữa các thành phần).
            if (WslPathNormalizer.IsWslNetworkUncPath(raw))
            {
                return WslPathNormalizer.NormalizeForWslpathArgument(raw);
            }

            string p = Path.GetFullPath(raw);
            return Directory.Exists(p) ? p : null;
        }

        return WslDockerServicePathResolver.TryFindFrom(appBaseDirectory);
    }

    /// <summary>
    /// Thư mục Windows dùng làm nguồn khi đồng bộ: ô «Nguồn trong Windows» nếu có; không thì giống <see cref="ResolveWindowsRoot"/>.
    /// </summary>
    private static string? ResolveWindowsSyncSourceRoot(AppSettings settings, string appBaseDirectory)
    {
        string? raw = settings.WslDockerServiceSyncSourceWindowsPath?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return ResolveWindowsRoot(settings, appBaseDirectory);
        }

        if (WslPathNormalizer.IsWslNetworkUncPath(raw))
        {
            return WslPathNormalizer.NormalizeForWslpathArgument(raw);
        }

        try
        {
            string p = Path.GetFullPath(raw);
            return Directory.Exists(p) ? p : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Ưu tiên tên trong cài đặt; nếu trống mà đường dẫn là UNC \\wsl.localhost\Distro\... thì lấy Distro (tránh wsl.exe dùng distro mặc định khác máy chứa mã).
    /// </summary>
    private static string? ResolveEffectiveDistribution(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.WslDistribution))
        {
            return settings.WslDistribution.Trim();
        }

        string? p = settings.WslDockerServiceWindowsPath?.Trim();
        return string.IsNullOrEmpty(p) ? null : TryGetDistroFromUncPath(p);
    }

    /// <summary>
    /// Trích tên distro từ \\wsl.localhost\Ubuntu-22.04\... hoặc \\wsl$\Ubuntu-22.04\...
    /// </summary>
    private static string? TryGetDistroFromUncPath(string windowsPath)
    {
        if (string.IsNullOrEmpty(windowsPath))
        {
            return null;
        }

        string n = windowsPath.Replace('/', '\\');
        const string prefix1 = @"\\wsl.localhost\";
        const string prefix2 = @"\\wsl$\";
        if (n.StartsWith(prefix1, StringComparison.OrdinalIgnoreCase))
        {
            string rest = n[prefix1.Length..];
            int slash = rest.IndexOf('\\');
            if (slash > 0)
            {
                return rest[..slash];
            }
        }
        else if (n.StartsWith(prefix2, StringComparison.OrdinalIgnoreCase))
        {
            string rest = n[prefix2.Length..];
            int slash = rest.IndexOf('\\');
            if (slash > 0)
            {
                return rest[..slash];
            }
        }

        return null;
    }

    /// <summary>
    /// Chuẩn hóa thông báo lỗi wslpath cho UI (kèm stderr từ wsl.exe nếu có).
    /// </summary>
    private static string FormatWslpathUserMessage(string leadSentence, string? probeError)
    {
        if (string.IsNullOrWhiteSpace(probeError))
        {
            return leadSentence
                + " Kiểm tra WSL đang chạy, tên distro trong Cài đặt, và thư mục Windows tồn tại.";
        }

        return leadSentence.TrimEnd() + " " + probeError.Trim();
    }

    private static bool TryGetWslUnixPath(
        string windowsDirectory,
        string? wslDistribution,
        out string wslPath,
        out string? wslpathError)
    {
        return WslPathProbe.TryWindowsToUnix(windowsDirectory, wslDistribution, out wslPath, out wslpathError);
    }

    /// <summary>
    /// Kiểm tra đường dẫn Unix trước khi đưa vào đối số `wsl.exe --cd`.
    /// Chặn các ký tự có thể gây chèn lệnh qua shell tương tác hoặc script wrapper.
    /// </summary>
    /// <exception cref="ArgumentException">Ném ra khi phát hiện ký tự cấm.</exception>
    public static void ValidateWslUnixPathForSpawn(string wslUnixPath)
    {
        if (string.IsNullOrWhiteSpace(wslUnixPath))
        {
            throw new ArgumentException("Đường dẫn WSL không được để trống.", nameof(wslUnixPath));
        }

        foreach (char bad in new[] { '\'', '"', '`', '$', '\n', '\r', ';', '|', '&', '<', '>' })
        {
            if (!wslUnixPath.Contains(bad))
            {
                continue;
            }

            string printable = bad switch
            {
                '\n' => "\\n",
                '\r' => "\\r",
                _ => bad.ToString()
            };
            throw new ArgumentException(
                "Đường dẫn WSL chứa ký tự không hợp lệ: " + printable,
                nameof(wslUnixPath));
        }
    }

    /// <param name="scriptRelativeFromRoot">Ví dụ scripts/run-server.sh, scripts/stop-server.sh, scripts/restart-server.sh.</param>
    private static void SpawnWslLifecycleScript(string wslUnixPath, string? distribution, string scriptRelativeFromRoot)
    {
        ValidateWslUnixPathForSpawn(wslUnixPath);
        var psi = new ProcessStartInfo
        {
            FileName = "wsl.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (!string.IsNullOrWhiteSpace(distribution))
        {
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(distribution.Trim());
        }
        psi.ArgumentList.Add("--cd");
        psi.ArgumentList.Add(wslUnixPath);
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add("bash");
        psi.ArgumentList.Add(scriptRelativeFromRoot);

        string distroLabel = string.IsNullOrWhiteSpace(distribution) ? "(mặc định)" : distribution.Trim();
        string cmdSummary = string.IsNullOrWhiteSpace(distribution)
            ? $"wsl.exe --cd {wslUnixPath} -- bash {scriptRelativeFromRoot}"
            : $"wsl.exe -d {distribution.Trim()} --cd {wslUnixPath} -- bash {scriptRelativeFromRoot}";

        lock (WslRecentOutputLock)
        {
            RecentWslLines.Clear();
            _lastWslCommandSummary = cmdSummary;
            _lastWslLaunchInfo = "Distro: " + distroLabel + ", thư mục WSL: " + wslUnixPath;
        }

        AppFileLog.Write(
            "WSL",
            "Khởi chạy: distro=" + distroLabel + " unixPath=" + wslUnixPath);

        try
        {
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                AppFileLog.Write("WSL stdout", e.Data);
                lock (WslRecentOutputLock)
                {
                    RecentWslLines.Add("[stdout] " + e.Data);
                    while (RecentWslLines.Count > MaxRecentWslLines)
                    {
                        RecentWslLines.RemoveAt(0);
                    }
                }
            };
            p.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                AppFileLog.Write("WSL stderr", e.Data);
                lock (WslRecentOutputLock)
                {
                    RecentWslLines.Add("[stderr] " + e.Data);
                    while (RecentWslLines.Count > MaxRecentWslLines)
                    {
                        RecentWslLines.RemoveAt(0);
                    }
                }
            };

            p.Start();
            lock (WslProcessLock)
            {
                WslRunServerProcesses.Add(p);
            }

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            AppFileLog.WriteException("WSL", ex);
        }
    }

    /// <summary>
    /// Vài lần kiểm tra health + Docker (GET /api/health ổn định rồi GET /api/docker/info) trước khi spawn WSL.
    /// </summary>
    private static async Task<bool> IsHealthOkWithRetryAsync(
        DockLiteHttpSession httpSession,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        const int attempts = 3;
        var between = TimeSpan.FromMilliseconds(250);
        for (int i = 0; i < attempts; i++)
        {
            if (await IsConnectivityStableOkAsync(httpSession, settings, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            if (i < attempts - 1)
            {
                await Task.Delay(between, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    /// <summary>
    /// Hai lần GET /api/health ổn định, sau đó GET /api/docker/info (cùng kiểu probe) — khớp trang Tổng quan và header.
    /// </summary>
    private static async Task<bool> IsConnectivityStableOkAsync(
        DockLiteHttpSession httpSession,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (!await IsHealthStableOkAsync(httpSession, settings, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return await IsDockerInfoOkOnceAsync(httpSession, settings, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Hai lần GET /api/health liên tiếp (kết nối đóng, đọc hết body) cách khoảng ngắn — tránh «probe giả ổn» sau resume WSL/TCP.
    /// </summary>
    private static async Task<bool> IsHealthStableOkAsync(
        DockLiteHttpSession httpSession,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (!await IsHealthOkOnceAsync(httpSession, settings, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
        return await IsHealthOkOnceAsync(httpSession, settings, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// GET /api/docker/info một lần (kết nối đóng, đọc JSON envelope) — cùng mức «sẵn sàng» với trang Tổng quan.
    /// </summary>
    private static async Task<bool> IsDockerInfoOkOnceAsync(
        DockLiteHttpSession httpSession,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        HttpClient httpClient = httpSession.Client;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(GetSingleHealthProbeTimeout(settings));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(httpClient.BaseAddress!, "api/docker/info"));
            request.Headers.ConnectionClose = true;
            using HttpResponseMessage response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);
            string text = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            ApiEnvelope<DockerInfoData>? env = JsonSerializer.Deserialize<ApiEnvelope<DockerInfoData>>(text, DockerInfoJsonOptions);
            return env is not null && env.Success && env.Data is not null;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task<bool> IsHealthOkOnceAsync(
        DockLiteHttpSession httpSession,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        HttpClient httpClient = httpSession.Client;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(GetSingleHealthProbeTimeout(settings));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(httpClient.BaseAddress!, "api/health"));
            request.Headers.ConnectionClose = true;
            using HttpResponseMessage response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);
            bool ok = response.IsSuccessStatusCode;
            try
            {
                await response.Content.CopyToAsync(Stream.Null, linked.Token).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }

            return ok;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
