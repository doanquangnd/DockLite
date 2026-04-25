using System.Text.Json;
using DockLite.Core.Configuration;

namespace DockLite.Tests;

/// <summary>
/// Kiểm tra AppSettings: token không nằm trong JSON (JsonIgnore trên thuộc tính bí mật).
/// </summary>
public sealed class AppSettingsStoreServiceApiTokenTests
{
    [Fact]
    public void AppSettings_JsonSerialize_Omits_SecretTokenValue()
    {
        const string secret = "khong-gui-vao-file-json-xyz";
        var s = new AppSettings
        {
            ServiceBaseUrl = "http://127.0.0.1:17890/",
            ServiceApiToken = secret,
            ServiceApiTokenProfile = "default",
        };
        string json = JsonSerializer.Serialize(
            s,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = null });
        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
    }
}
