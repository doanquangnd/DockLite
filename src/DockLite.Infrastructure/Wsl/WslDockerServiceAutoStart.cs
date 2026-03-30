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

    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan HealthPollTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ManualStartHealthWait = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan SingleHealthTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Kiểm tra GET api/health; nếu lỗi và bật tự khởi động thì chạy WSL và chờ service sẵn sàng.
    /// </summary>
    /// <returns>Ok nếu cuối cùng health OK; Reason mô tả khi không đạt.</returns>
    public static async Task<(bool Ok, WslEnsureFailureReason Reason)> TryEnsureRunningAsync(
        DockLiteHttpSession httpSession,
        AppSettings settings,
        string appBaseDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!settings.AutoStartWslService)
        {
            bool healthOk = await IsHealthOkWithRetryAsync(httpSession, cancellationToken).ConfigureAwait(false);
            return (
                healthOk,
                healthOk ? WslEnsureFailureReason.None : WslEnsureFailureReason.HealthUnavailableWhenAutoStartOff);
        }

        if (await IsHealthOkWithRetryAsync(httpSession, cancellationToken).ConfigureAwait(false))
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

        StartWslRunServer(wslPath, distro);

        DateTime deadline = DateTime.UtcNow + HealthPollTimeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(HealthPollInterval, cancellationToken).ConfigureAwait(false);
            if (await IsHealthOkOnceAsync(httpSession, cancellationToken).ConfigureAwait(false))
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
            StartWslRunServer(wslPath, distro);
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
    /// Gửi lệnh khởi động WSL rồi chờ GET /api/health thành công (tối đa khoảng 90 giây).
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

        DateTime deadline = DateTime.UtcNow + ManualStartHealthWait;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(HealthPollInterval, cancellationToken).ConfigureAwait(false);
            if (await IsHealthOkOnceAsync(httpSession, cancellationToken).ConfigureAwait(false))
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
            "Đã gửi lệnh tới WSL nhưng sau khoảng 90 giây vẫn không kết nối được health. "
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
            if (LooksLikeWslUncPath(raw))
            {
                return raw;
            }

            string p = Path.GetFullPath(raw);
            return Directory.Exists(p) ? p : null;
        }

        return WslDockerServicePathResolver.TryFindFrom(appBaseDirectory);
    }

    private static bool LooksLikeWslUncPath(string p)
    {
        if (string.IsNullOrEmpty(p))
        {
            return false;
        }

        string n = p.Replace('/', '\\');
        return n.StartsWith(@"\\wsl.localhost\", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith(@"\\wsl$\", StringComparison.OrdinalIgnoreCase);
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
        string full = Path.GetFullPath(windowsDirectory);
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

    private static void StartWslRunServer(string wslUnixPath, string? distribution)
    {
        // Gọi qua bash thay vì ./run-server.sh để không phụ thuộc chmod +x (tránh Permission denied trên clone/NTFS).
        // Dùng -lc (login shell): bash -c không nạp .profile/.bashrc — go thường nằm trong PATH chỉ sau login/interactive.
        string inner = $"cd '{wslUnixPath}' && exec bash scripts/run-server.sh";
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
            ? $"wsl.exe bash -lc \"cd '{wslUnixPath}' && exec bash scripts/run-server.sh\""
            : $"wsl.exe -d {distribution.Trim()} bash -lc \"cd '{wslUnixPath}' && exec bash scripts/run-server.sh\"";

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
        CancellationToken cancellationToken)
    {
        const int attempts = 3;
        var between = TimeSpan.FromMilliseconds(250);
        for (int i = 0; i < attempts; i++)
        {
            if (await IsHealthOkOnceAsync(httpSession, cancellationToken).ConfigureAwait(false))
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

    private static async Task<bool> IsHealthOkOnceAsync(DockLiteHttpSession httpSession, CancellationToken cancellationToken)
    {
        HttpClient httpClient = httpSession.Client;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(SingleHealthTimeout);
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
