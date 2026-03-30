using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace DockLite.Infrastructure.Wsl;

/// <summary>
/// Lấy địa chỉ IPv4 của distro WSL (hostname -I) để dùng khi Windows không forward 127.0.0.1 vào WSL2.
/// </summary>
public static class WslHostAddressResolver
{
    /// <summary>
    /// Chạy wsl hostname -I, lấy IPv4 đầu tiên.
    /// </summary>
    public static bool TryGetFirstIpv4(string? wslDistribution, out string ipv4)
    {
        ipv4 = "";
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

        p.StartInfo.ArgumentList.Add("hostname");
        p.StartInfo.ArgumentList.Add("-I");

        try
        {
            p.Start();
            string? output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(15000);
            if (p.ExitCode != 0)
            {
                return false;
            }

            foreach (string token in output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = token.Trim();
                if (IPAddress.TryParse(t, out IPAddress? addr) && addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipv4 = t;
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
