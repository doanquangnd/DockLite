using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;

namespace DockLite.App.Services;

/// <summary>
/// Ánh xạ ngoại lệ liên quan mạng/HTTP sang chuỗi hiển thị đã dịch (tài nguyên <c>UiStrings.*.xaml</c>).
/// </summary>
public static class NetworkErrorMessageMapper
{
    private const int MaxDepth = 5;

    /// <summary>
    /// Trả về mô tả lỗi phù hợp để hiển thị trên UI hoặc hộp thoại (theo ngôn ngữ giao diện hiện tại).
    /// </summary>
    public static string FormatForUser(Exception ex) => FormatForUser(ex, 0);

    private static string FormatForUser(Exception ex, int depth)
    {
        if (depth > MaxDepth)
        {
            return ex.Message;
        }

        if (ex is AggregateException ae && ae.InnerExceptions.Count == 1)
        {
            return FormatForUser(ae.InnerExceptions[0], depth);
        }

        // Lỗi tầng HTTP (gồm HTTP/2 stream) — cùng gợi ý kết nối tới service.
        if (ex is HttpIOException)
        {
            return HttpConnectionMessage();
        }

        if (ex is TaskCanceledException tce)
        {
            if (tce.CancellationToken.IsCancellationRequested)
            {
                return CancelledMessage();
            }

            return TimeoutMessage();
        }

        if (ex is HttpRequestException hre)
        {
            return HttpRequestExceptionMessage(hre);
        }

        if (ex is SocketException)
        {
            return HttpConnectionMessage();
        }

        if (ex is WebException we)
        {
            return WebExceptionMessage(we);
        }

        if (ex is AuthenticationException)
        {
            return TlsOrCertificateMessage();
        }

        if (ex is IOException ioex && LooksLikeTransportIOException(ioex))
        {
            return HttpConnectionMessage();
        }

        if (ex is InvalidOperationException ioe
            && ioe.Message.Contains("HttpReadRetry", StringComparison.Ordinal))
        {
            return HttpConnectionMessage() + HttpReadRetrySuffix();
        }

        if (ex is OperationCanceledException)
        {
            return CancelledMessage();
        }

        if (ex.InnerException is not null)
        {
            string innerFormatted = FormatForUser(ex.InnerException, depth + 1);
            if (!string.Equals(innerFormatted, ex.InnerException.Message, StringComparison.Ordinal))
            {
                return innerFormatted;
            }
        }

        return ex.Message;
    }

    private static string WebExceptionMessage(WebException we)
    {
        return we.Status switch
        {
            WebExceptionStatus.Timeout => TimeoutMessage(),
            WebExceptionStatus.RequestCanceled => CancelledMessage(),
            _ => HttpConnectionMessage(),
        };
    }

    /// <summary>
    /// Một số đường dẫn hiếm (HttpClient, stream) ném <see cref="IOException"/> với thông điệp tiếng Anh cố định thay vì <see cref="SocketException"/>.
    /// </summary>
    private static bool LooksLikeTransportIOException(IOException ioex)
    {
        string m = ioex.Message ?? string.Empty;
        return m.Contains("transport connection", StringComparison.OrdinalIgnoreCase)
            || m.Contains("Unable to read data", StringComparison.OrdinalIgnoreCase)
            || m.Contains("Unable to write data", StringComparison.OrdinalIgnoreCase)
            || m.Contains("prematurely", StringComparison.OrdinalIgnoreCase)
            || m.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase);
    }

    private static string CancelledMessage() =>
        UiLanguageManager.TryLocalizeCurrent("Ui_Status_Common_CancelledShort", "Đã hủy.");

    private static string TimeoutMessage() =>
        UiLanguageManager.TryLocalizeCurrent(
            "Ui_Error_Network_Timeout",
            "Hết thời gian chờ (timeout). Kiểm tra service WSL đã chạy hoặc tăng timeout trong Cài đặt.");

    private static string HttpRequestExceptionMessage(HttpRequestException hre)
    {
        HttpStatusCode? code = hre.StatusCode;
        if (code == HttpStatusCode.Unauthorized)
        {
            return UnauthorizedMessage();
        }

        if (code == HttpStatusCode.Forbidden)
        {
            return ForbiddenMessage();
        }

        if (code == HttpStatusCode.NotFound)
        {
            return NotFoundMessage();
        }

        if (code == HttpStatusCode.RequestTimeout)
        {
            return RequestTimeoutHttpMessage();
        }

        if (code == HttpStatusCode.TooManyRequests)
        {
            return TooManyRequestsMessage();
        }

        if (code == HttpStatusCode.InternalServerError)
        {
            return ServerErrorMessage();
        }

        if (code == HttpStatusCode.BadGateway)
        {
            return BadGatewayMessage();
        }

        if (code == HttpStatusCode.ServiceUnavailable)
        {
            return ServiceUnavailableMessage();
        }

        return HttpConnectionMessage();
    }

    private static string UnauthorizedMessage() =>
        UiLanguageManager.TryLocalizeCurrent(
            "Ui_Error_Network_Unauthorized",
            "Từ chối truy cập (401). Kiểm tra token API trong Cài đặt khớp với biến DOCKLITE_API_TOKEN trên service WSL, rồi Lưu.");

    private static string ForbiddenMessage() =>
        UiLanguageManager.TryLocalizeCurrent(
            "Ui_Error_Network_Forbidden",
            "Truy cập bị cấm (403). Kiểm tra quyền hoặc cấu hình reverse proxy / token.");

    private static string NotFoundMessage() =>
        UiLanguageManager.TryLocalizeCurrent(
            "Ui_Error_Network_NotFound",
            "Không tìm thấy tài nguyên (404). Kiểm tra đường dẫn API hoặc ID container/image đã bị xóa.");

    private static string RequestTimeoutHttpMessage() =>
        UiLanguageManager.TryLocalizeCurrent(
            "Ui_Error_Network_RequestTimeoutHttp",
            "Hết thời gian chờ phía server (408). Service hoặc Docker có thể quá tải; thử lại hoặc tăng timeout trong Cài đặt.");

    private static string TooManyRequestsMessage() =>
        UiLanguageManager.TryLocalizeCurrent(
            "Ui_Error_Network_TooManyRequests",
            "Quá nhiều yêu cầu (429). Đợi vài giây rồi thử lại.");

    private static string ServerErrorMessage() =>
        UiLanguageManager.TryLocalizeCurrent(
            "Ui_Error_Network_ServerError",
            "Lỗi nội bộ service (500). Xem log service Go trong WSL hoặc nhật ký ứng dụng.");

    private static string BadGatewayMessage() =>
        UiLanguageManager.TryLocalizeCurrent(
            "Ui_Error_Network_BadGateway",
            "Bad gateway (502). Reverse proxy ho Docker Engine có thể không phản hồi; kiểm tra WSL và Docker.");

    private static string ServiceUnavailableMessage() =>
        UiLanguageManager.TryLocalizeCurrent(
            "Ui_Error_Network_ServiceUnavailable",
            "Dịch vụ tạm không sẵn sàng (503). Docker Engine có thể chưa chạy hoặc service đang khởi động.");

    private static string HttpConnectionMessage() =>
        UiLanguageManager.TryLocalizeCurrent(
            "Ui_Error_Network_HttpConnection",
            "Không kết nối được tới service WSL. Kiểm tra binary Go đã chạy trong WSL và địa chỉ trong Cài đặt. "
            + "Trên WSL2, nếu service đã chạy mà vẫn lỗi với 127.0.0.1, dùng nút Điền IP WSL trong Cài đặt hoặc đặt base URL thành http://IP:17890/ với IP từ lệnh wsl hostname -I trên Windows (localhost đôi khi không được chuyển tiếp).");

    private static string HttpReadRetrySuffix() =>
        UiLanguageManager.TryLocalizeCurrent(
            "Ui_Error_Network_HttpReadRetrySuffix",
            " (Đã thử lại vài lần GET mà vẫn lỗi.)");

    private static string TlsOrCertificateMessage() =>
        UiLanguageManager.TryLocalizeCurrent(
            "Ui_Error_Network_TlsOrCertificate",
            "Lỗi TLS hoặc chứng chỉ. Kiểm tra HTTPS, CA tin cậy trên Windows và đồng hồ hệ thống.");
}
