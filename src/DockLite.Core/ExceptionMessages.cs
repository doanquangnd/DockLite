using System.Net.Http;

namespace DockLite.Core;

/// <summary>
/// Chuyển ngoại lệ kỹ thuật thành thông điệp ngắn gọn cho người dùng (tiếng Việt).
/// </summary>
public static class ExceptionMessages
{
    /// <summary>
    /// Trả về mô tả lỗi phù hợp để hiển thị trên UI hoặc hộp thoại.
    /// </summary>
    public static string FormatForUser(Exception ex)
    {
        if (ex is TaskCanceledException tce)
        {
            if (tce.CancellationToken.IsCancellationRequested)
            {
                return "Đã hủy.";
            }

            return "Hết thời gian chờ (timeout). Kiểm tra service WSL đã chạy hoặc tăng timeout trong Cài đặt.";
        }

        if (ex is HttpRequestException)
        {
            return "Không kết nối được tới service WSL. Kiểm tra binary Go đã chạy trong WSL và địa chỉ trong Cài đặt. "
                + "Trên WSL2, nếu service đã chạy mà vẫn lỗi với 127.0.0.1, dùng nút Điền IP WSL trong Cài đặt hoặc đặt base URL thành http://IP:17890/ với IP từ lệnh wsl hostname -I trên Windows (localhost đôi khi không được chuyển tiếp).";
        }

        if (ex is OperationCanceledException)
        {
            return "Đã hủy.";
        }

        return ex.Message;
    }
}
