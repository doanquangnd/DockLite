using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace DockLite.Infrastructure.Wsl;

/// <summary>
/// Gọi <c>wsl.exe wslpath -a</c> để chuyển đường dẫn Windows sang Unix trong distro.
/// </summary>
public static class WslPathProbe
{
    /// <summary>
    /// Thử chuyển đường dẫn Windows (file hoặc thư mục) sang dạng WSL.
    /// </summary>
    public static bool TryWindowsToUnix(string windowsPath, string? wslDistribution, out string unixPath, out string? errorMessage)
    {
        unixPath = string.Empty;
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(windowsPath))
        {
            errorMessage = "Đường dẫn trống.";
            return false;
        }

        string full;
        try
        {
            full = WslPathNormalizer.NormalizeForWslpathArgument(windowsPath);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }

        // UNC tới ổ Linux trong distro: dựng /home/... từ segment sau tên distro (wslpath hay trả sai).
        if (WslPathNormalizer.IsWslNetworkUncPath(full))
        {
            if (WslPathNormalizer.TryUnixPathFromWslUnc(full, wslDistribution, out unixPath, out string? hint))
            {
                errorMessage = null;
                return true;
            }

            errorMessage = hint ?? "Không đổi được đường UNC \\wsl.localhost\\... hoặc \\wsl$\\... sang đường trong distro.";
            return false;
        }

        using var p = new Process();
        p.StartInfo.FileName = "wsl.exe";
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
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
            // Đọc song song stdout/stderr để tránh deadlock khi pipe đầy (wslpath in thường ngắn nhưng an toàn hơn).
            Task<string> stdoutTask = Task.Run(() => p.StandardOutput.ReadToEnd());
            Task<string> stderrTask = Task.Run(() => p.StandardError.ReadToEnd());
            if (!p.WaitForExit(15000))
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Bỏ qua: tiến trình có thể đã thoát.
                }

                errorMessage = "wslpath timeout sau 15 giây.";
                return false;
            }

            string stdout = stdoutTask.GetAwaiter().GetResult();
            string err = stderrTask.GetAwaiter().GetResult().Trim();
            string? line = null;
            foreach (string part in stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = part.Trim();
                if (t.Length > 0)
                {
                    line = t;
                    break;
                }
            }

            if (p.ExitCode != 0 || string.IsNullOrEmpty(line))
            {
                errorMessage = string.IsNullOrEmpty(err) ? "wslpath thất bại (mã " + p.ExitCode + ")." : err;
                return false;
            }

            unixPath = line;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
