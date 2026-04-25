using System.Runtime.Versioning;
using DockLite.Core.Configuration;
using Windows.Security.Credentials;

namespace DockLite.Infrastructure.Configuration;

/// <summary>
/// Lưu token API trong Trình quản lý thông tin xác thực Windows (resource = DockLite:ServiceApiToken: + hồ sơ).
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class WindowsServiceApiTokenStore : IServiceApiTokenStore
{
    private const string UserName = "DockLiteClient";

    private static string ResourceName(string profile) => "DockLite:ServiceApiToken:" + profile;

    /// <inheritdoc />
    public string? Read(string profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            profile = "default";
        }

        var vault = new PasswordVault();
        try
        {
            PasswordCredential c = vault.Retrieve(ResourceName(profile), UserName);
            c.RetrievePassword();
            return c.Password;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Write(string profile, string? token)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            profile = "default";
        }

        var vault = new PasswordVault();
        string res = ResourceName(profile);
        try
        {
            PasswordCredential c = vault.Retrieve(res, UserName);
            vault.Remove(c);
        }
        catch
        {
        }

        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        vault.Add(new PasswordCredential(res, UserName, token));
    }

    /// <inheritdoc />
    public void Remove(string profile) => Write(profile, null);
}
