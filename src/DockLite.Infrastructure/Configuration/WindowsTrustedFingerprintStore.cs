using System.Globalization;
using System.Runtime.Versioning;
using DockLite.Core.Security;
using Windows.Security.Credentials;

namespace DockLite.Infrastructure.Configuration;

/// <summary>
/// Mục tài nguyên Credential: DockLite:TrustedFingerprint: + (host theo chuỗi URI) + : + port.
/// Với host IPv6 (nhiều dấu :), dùng chuỗi Host của Uri (ví dụ ::1) — Windows Credential resource chấp nhận; tránh mơ hồ bằng cách tách từ cùng Base URL ở client.
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class WindowsTrustedFingerprintStore : ITrustedFingerprintStore
{
    private const string UserName = "DockLiteTls";

    private static string ResourceName(string host, int port) =>
        "DockLite:TrustedFingerprint:" + Uri.EscapeDataString(host) + ":" + port.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public string? Read(string host, int port)
    {
        if (string.IsNullOrEmpty(host))
        {
            return null;
        }

        var vault = new PasswordVault();
        try
        {
            PasswordCredential c = vault.Retrieve(ResourceName(host, port), UserName);
            c.RetrievePassword();
            return c.Password;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Write(string host, int port, string sha256HexColon)
    {
        if (string.IsNullOrEmpty(host))
        {
            return;
        }

        var vault = new PasswordVault();
        string res = ResourceName(host, port);
        try
        {
            PasswordCredential c = vault.Retrieve(res, UserName);
            vault.Remove(c);
        }
        catch
        {
        }

        if (string.IsNullOrEmpty(sha256HexColon))
        {
            return;
        }

        vault.Add(new PasswordCredential(res, UserName, sha256HexColon));
    }

    /// <inheritdoc />
    public void Remove(string host, int port) => Write(host, port, string.Empty);
}
