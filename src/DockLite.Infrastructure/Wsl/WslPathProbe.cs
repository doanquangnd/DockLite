using System.Diagnostics;
using System.Text;

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
            full = Path.GetFullPath(windowsPath.Trim());
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
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
            string? line = p.StandardOutput.ReadLine()?.Trim();
            string err = p.StandardError.ReadToEnd().Trim();
            p.WaitForExit(15000);
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
