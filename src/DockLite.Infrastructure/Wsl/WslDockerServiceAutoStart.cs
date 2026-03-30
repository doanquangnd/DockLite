using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using DockLite.Core.Configuration;
using DockLite.Core.Diagnostics;
using DockLite.Infrastructure.Api;

namespace DockLite.Infrastructure.Wsl;

/// <summary>
/// Nếu service chưa phản hồi, gọi wsl.exe chạy bash scripts/run-server.sh trong thư mục wsl-docker-service (không dùng ./ để tránh Permission denied).
/// </summary>
public static class WslDockerServiceAutoStart
{
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
    /// Kiểm tra GET api/health; nếu lỗi và bật tự khởi động thì chạy WSL và chờ service sẵn sàng.
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
            return (
                healthOk,
                healthOk ? WslEnsureFailureReason.None : WslEnsureFailureReason.HealthUnavailableWhenAutoStartOff);
        }

        progress?.Report(new WslStartupProgress(WslStartupPhase.CheckingInitialHealth, null));
        if (await IsHealthOkWithRetryAsync(httpSession, settings, cancellationToken).ConfigureAwait(false))
        {
            return (true, WslEnsureFailureReason.None);
        }

        string? root = ResolveWindowsRoot(settings, appBaseDirectory);
        if (string.IsNullOrEmpty(root))
        {
            Debug.WriteLine("DockLite: không xác định được thư mục wsl-docker-service (cấu hình hoặc tìm tự động).");
            return (false, WslEnsureFailureReason.MissingServiceRoot);
        }

        string runScript = Path.Combine(root, "scripts", "run-server.sh");
        if (!File.Exists(runScript))
        {
            Debug.WriteLine("DockLite: thiếu scripts/run-server.sh tại " + root);
            return (false, WslEnsureFailureReason.MissingRunScript);
        }

        string? distro = ResolveEffectiveDistribution(settings);
        if (!TryGetWslUnixPath(root, distro, out string wslPath))
        {
            Debug.WriteLine("DockLite: wslpath thất bại cho " + root);
            return (false, WslEnsureFailureReason.WslPathConversionFailed);
        }

        progress?.Report(new WslStartupProgress(WslStartupPhase.LaunchingWslScript, null));
        SpawnWslLifecycleScript(wslPath, distro, "scripts/run-server.sh");

        TimeSpan waitTotal = TimeSpan.FromSeconds(GetHealthWaitAfterWslSeconds(settings));
        DateTime deadline = DateTime.UtcNow + waitTotal;
        TimeSpan pollInterval = GetHealthPollInterval(settings);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int remaining = Math.Max(0, (int)Math.Ceiling((deadline - DateTime.UtcNow).TotalSeconds));
            progress?.Report(new WslStartupProgress(WslStartupPhase.WaitingHealthAfterWsl, remaining));
            await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            if (await IsHealthOkOnceAsync(httpSession, settings, cancellationToken).ConfigureAwait(false))
            {
                return (true, WslEnsureFailureReason.None);
            }
        }

        string hint = FormatHealthTimeoutUserHint(AppFileLog.LogDirectory);
        AppFileLog.WriteMultiline("WSL health timeout", hint);
        return (false, WslEnsureFailureReason.HealthTimeoutAfterWslStart);
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
                sb.AppendLine("Không nhận /api/health trong thời gian chờ sau khi gọi WSL.");
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
        if (!TryGetWslUnixPath(root, distro, out string wslPath))
        {
            userMessage =
                "Không chuyển được đường dẫn sang Unix (wslpath). Kiểm tra WSL và đường dẫn thư mục.";
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
        if (!TryGetWslUnixPath(root, distro, out string wslPath))
        {
            userMessage =
                "Không chuyển được đường dẫn sang Unix (wslpath). Kiểm tra WSL và đường dẫn thư mục.";
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
        if (!TryGetWslUnixPath(root, distro, out string wslPath))
        {
            userMessage =
                "Không chuyển được đường dẫn sang Unix (wslpath). Kiểm tra WSL và đường dẫn thư mục.";
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
        if (!TryGetWslUnixPath(root, distro, out string wslPath))
        {
            userMessage =
                "Không chuyển được đường dẫn sang Unix (wslpath). Kiểm tra WSL và đường dẫn thư mục.";
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
            if (await IsHealthOkOnceAsync(httpSession, settings, cancellationToken).ConfigureAwait(false))
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
            if (await IsHealthOkOnceAsync(httpSession, settings, cancellationToken).ConfigureAwait(false))
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

    private static bool TryGetWslUnixPath(string windowsDirectory, string? wslDistribution, out string wslPath)
    {
        wslPath = "";
        string full = WslPathNormalizer.NormalizeForWslpathArgument(windowsDirectory);

        // UNC \\wsl.localhost\Distro\... : không dùng wslpath (thường trả /mnt/c/wsl.localhost/... không tồn tại).
        if (WslPathNormalizer.IsWslNetworkUncPath(full))
        {
            return WslPathNormalizer.TryUnixPathFromWslUnc(full, wslDistribution, out wslPath, out _);
        }

        using var p = new Process();
        p.StartInfo.FileName = "wsl.exe";
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.CreateNoWindow = true;
        if (!string.IsNullOrWhiteSpace(wslDistribution))
        {
            p.StartInfo.ArgumentList.Add("-d");
            p.StartInfo.ArgumentList.Add(wslDistribution.Trim());
        }

        p.StartInfo.ArgumentList.Add("wslpath");
        p.StartInfo.ArgumentList.Add("-a");
        p.StartInfo.ArgumentList.Add(full);
        try
        {
            p.Start();
            string? line = p.StandardOutput.ReadLine()?.Trim();
            p.WaitForExit(15000);
            if (p.ExitCode != 0 || string.IsNullOrEmpty(line))
            {
                return false;
            }

            wslPath = line;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <param name="scriptRelativeFromRoot">Ví dụ scripts/run-server.sh, scripts/stop-server.sh, scripts/restart-server.sh.</param>
    private static void SpawnWslLifecycleScript(string wslUnixPath, string? distribution, string scriptRelativeFromRoot)
    {
        // Gọi qua bash thay vì ./script.sh để không phụ thuộc chmod +x (tránh Permission denied trên clone/NTFS).
        // Dùng -lc (login shell): bash -c không nạp .profile/.bashrc — go thường nằm trong PATH chỉ sau login/interactive.
        string inner = $"cd '{wslUnixPath}' && exec bash {scriptRelativeFromRoot}";
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

        if (string.IsNullOrWhiteSpace(distribution))
        {
            psi.ArgumentList.Add("bash");
            psi.ArgumentList.Add("-lc");
            psi.ArgumentList.Add(inner);
        }
        else
        {
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(distribution.Trim());
            psi.ArgumentList.Add("bash");
            psi.ArgumentList.Add("-lc");
            psi.ArgumentList.Add(inner);
        }

        string distroLabel = string.IsNullOrWhiteSpace(distribution) ? "(mặc định)" : distribution.Trim();
        string cmdSummary = string.IsNullOrWhiteSpace(distribution)
            ? $"wsl.exe bash -lc \"cd '{wslUnixPath}' && exec bash {scriptRelativeFromRoot}\""
            : $"wsl.exe -d {distribution.Trim()} bash -lc \"cd '{wslUnixPath}' && exec bash {scriptRelativeFromRoot}\"";

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
    /// Vài lần GET /api/health cách nhau (dùng trước khi spawn WSL, không dùng mỗi vòng chờ dài).
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
            if (await IsHealthOkOnceAsync(httpSession, settings, cancellationToken).ConfigureAwait(false))
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
            using HttpResponseMessage response = await httpClient
                .GetAsync(new Uri(httpClient.BaseAddress!, "api/health"), linked.Token)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
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
