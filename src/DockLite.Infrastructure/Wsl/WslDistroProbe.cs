using System.Diagnostics;
using System.Text;

namespace DockLite.Infrastructure.Wsl;

/// <summary>
/// Gọi lệnh trong distro WSL (kiểm tra distro, v.v.).
/// </summary>
public static class WslDistroProbe
{
    /// <summary>
    /// Chạy <c>uname -a</c> trong distro (hoặc distro mặc định nếu <paramref name="wslDistribution"/> trống).
    /// </summary>
    public static bool TryRunUname(string? wslDistribution, out string stdout, out string? errorMessage)
    {
        stdout = string.Empty;
        errorMessage = null;
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

        p.StartInfo.ArgumentList.Add("--");
        p.StartInfo.ArgumentList.Add("uname");
        p.StartInfo.ArgumentList.Add("-a");
        try
        {
            p.Start();
            stdout = p.StandardOutput.ReadToEnd().Trim();
            string err = p.StandardError.ReadToEnd().Trim();
            p.WaitForExit(20000);
            if (p.ExitCode != 0)
            {
                errorMessage = string.IsNullOrEmpty(err) ? "uname thoát với mã " + p.ExitCode + "." : err;
                return false;
            }

            if (string.IsNullOrEmpty(stdout))
            {
                errorMessage = string.IsNullOrEmpty(err) ? "Không có đầu ra." : err;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
