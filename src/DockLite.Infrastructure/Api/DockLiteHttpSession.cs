using System.Net.Http;
using System.Net.WebSockets;
using DockLite.Core.Configuration;
using DockLite.Core.Diagnostics;
using DockLite.Core.Security;

namespace DockLite.Infrastructure.Api;

/// <summary>
/// Giữ một <see cref="HttpClient"/> có thể thay toàn bộ sau khi Lưu cài đặt (không được đổi BaseAddress/Timeout trên instance đã gửi request).
/// </summary>
public sealed class DockLiteHttpSession
{
    private readonly ITrustedFingerprintStore? _fingerprintStore;
    private HttpClient _client;

    public DockLiteHttpSession(AppSettings settings, ITrustedFingerprintStore? fingerprintStore = null)
    {
        _fingerprintStore = fingerprintStore;
        _client = CreateClient(settings);
    }

    private HttpClient CreateClient(AppSettings settings)
    {
        HttpMessageHandler inner = CreateInnerHandler(settings, _fingerprintStore);
        var requestIdHandler = new RequestIdDelegatingHandler { InnerHandler = inner };
        var client = new HttpClient(requestIdHandler, disposeHandler: true);
        HttpClientAppSettings.ApplyTo(client, settings);
        return client;
    }

    private static HttpMessageHandler CreateInnerHandler(AppSettings settings, ITrustedFingerprintStore? store)
    {
        Uri b = ServiceBaseUriHelper.Normalize(settings.ServiceBaseUrl);
        if (b.Scheme == Uri.UriSchemeHttps && store is not null)
        {
            var h = new HttpClientHandler { UseProxy = false };
            h.ServerCertificateCustomValidationCallback = (message, cert, chain, e) =>
            {
                if (message?.RequestUri is not { } ru || !ru.IsAbsoluteUri)
                {
                    return false;
                }

                return DockLiteTlsClientValidation.ServerCertificateValidatesPin(
                    store,
                    ru.Host,
                    ru.Port,
                    cert,
                    chain,
                    e);
            };
            return h;
        }

        return new SocketsHttpHandler { UseProxy = false };
    }

    /// <summary>
    /// Client hiện dùng cho API và WebSocket base URL.
    /// </summary>
    public HttpClient Client => _client;

    /// <summary>
    /// Gắn xác thực cert WSS trùng pin với HTTPS (sau khi tạo <see cref="ClientWebSocket"/>, trước <see cref="ClientWebSocket.ConnectAsync"/>).
    /// </summary>
    public void ApplyTlsToClientWebSocketIfNeeded(ClientWebSocket webSocket)
    {
        if (_fingerprintStore is null)
        {
            return;
        }

        if (Client.BaseAddress?.Scheme != Uri.UriSchemeHttps)
        {
            return;
        }

        webSocket.Options.RemoteCertificateValidationCallback = (uriObj, cert, chain, e) =>
        {
            if (uriObj is not Uri uri)
            {
                return false;
            }

            return DockLiteTlsClientValidation.ServerCertificateValidatesPin(
                _fingerprintStore,
                uri.Host,
                uri.Port,
                cert,
                chain,
                e);
        };
    }

    /// <summary>
    /// Tạo HttpClient mới theo cấu hình (gọi khi người dùng Lưu trong Cài đặt).
    /// Client cũ được dispose trễ để tránh ObjectDisposedException khi vòng health/WSL hoặc request vẫn dùng instance cũ.
    /// </summary>
    public void Reconfigure(AppSettings settings)
    {
        HttpClient oldClient = _client;
        _client = CreateClient(settings);

        TimeSpan delay = oldClient.Timeout + TimeSpan.FromSeconds(2);
        if (delay > TimeSpan.FromMinutes(12))
        {
            delay = TimeSpan.FromMinutes(12);
        }

        AppFileLog.Write(
            "Http",
            "Đã thay HttpClient (client cũ dispose sau khoảng " + (int)delay.TotalSeconds + " giây).");

        _ = Task.Run(async () =>
        {
            await Task.Delay(delay).ConfigureAwait(false);
            try
            {
                oldClient.Dispose();
            }
            catch
            {
                // bỏ qua
            }
        });
    }
}
