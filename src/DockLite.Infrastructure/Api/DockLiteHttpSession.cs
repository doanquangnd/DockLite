using System.Net.Http;
using DockLite.Core.Configuration;
using DockLite.Core.Diagnostics;

namespace DockLite.Infrastructure.Api;

/// <summary>
/// Giữ một <see cref="HttpClient"/> có thể thay toàn bộ sau khi Lưu cài đặt (không được đổi BaseAddress/Timeout trên instance đã gửi request).
/// </summary>
public sealed class DockLiteHttpSession
{
    private HttpClient _client;

    public DockLiteHttpSession(AppSettings settings)
    {
        _client = CreateClient(settings);
    }

    private static HttpClient CreateClient(AppSettings settings)
    {
        // Tắt proxy hệ thống: một số môi trường chặn hoặc sai hướng kết nối tới localhost / WSL.
        var handler = new SocketsHttpHandler { UseProxy = false };
        var client = new HttpClient(handler, disposeHandler: true);
        HttpClientAppSettings.ApplyTo(client, settings);
        return client;
    }

    /// <summary>
    /// Client hiện dùng cho API và WebSocket base URL.
    /// </summary>
    public HttpClient Client => _client;

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
