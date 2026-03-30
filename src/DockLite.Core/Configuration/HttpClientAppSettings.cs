using System.Net.Http;

namespace DockLite.Core.Configuration;

/// <summary>
/// Áp dụng <see cref="AppSettings"/> lên <see cref="HttpClient"/> (base URL, timeout).
/// </summary>
public static class HttpClientAppSettings
{
    /// <summary>
    /// Gán BaseAddress và Timeout từ cấu hình (timeout giới hạn 30–600 giây).
    /// </summary>
    public static void ApplyTo(HttpClient client, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(settings);

        int timeoutSec = settings.HttpTimeoutSeconds >= 30 ? settings.HttpTimeoutSeconds : 120;
        timeoutSec = Math.Clamp(timeoutSec, 30, 600);
        client.Timeout = TimeSpan.FromSeconds(timeoutSec);
        client.BaseAddress = ServiceBaseUriHelper.Normalize(settings.ServiceBaseUrl);
    }
}
