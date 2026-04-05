using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;

namespace DockLite.Core.Configuration;

/// <summary>
/// Áp dụng <see cref="AppSettings"/> lên <see cref="HttpClient"/> (base URL, timeout, Bearer token).
/// </summary>
public static class HttpClientAppSettings
{
    /// <summary>
    /// Gán BaseAddress, Timeout và header Authorization (nếu có token) từ cấu hình (timeout giới hạn 30–600 giây).
    /// </summary>
    public static void ApplyTo(HttpClient client, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(settings);

        int timeoutSec = settings.HttpTimeoutSeconds >= 30 ? settings.HttpTimeoutSeconds : 120;
        timeoutSec = Math.Clamp(timeoutSec, 30, 600);
        client.Timeout = TimeSpan.FromSeconds(timeoutSec);
        client.BaseAddress = ServiceBaseUriHelper.Normalize(settings.ServiceBaseUrl);

        client.DefaultRequestHeaders.Authorization = null;
        if (!string.IsNullOrWhiteSpace(settings.ServiceApiToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                settings.ServiceApiToken.Trim());
        }
    }

    /// <summary>
    /// Sao chép header Authorization từ <see cref="HttpClient"/> sang <see cref="ClientWebSocket"/> (WebSocket không dùng DefaultRequestHeaders của HttpClient).
    /// </summary>
    public static void CopyAuthorizationToWebSocket(ClientWebSocket webSocket, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(webSocket);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (httpClient.DefaultRequestHeaders.Authorization is { } auth)
        {
            webSocket.Options.SetRequestHeader("Authorization", auth.ToString());
        }

        // Một ID mỗi phiên WebSocket — gỡ lỗi song song với log service (req_id / X-Request-ID).
        webSocket.Options.SetRequestHeader("X-Request-ID", Guid.NewGuid().ToString("N"));
    }
}
