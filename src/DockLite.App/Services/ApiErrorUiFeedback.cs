using System.Threading;
using System.Threading.Tasks;

namespace DockLite.App.Services;

/// <summary>
/// Toast cảnh báo kèm chuỗi đã map <see cref="NetworkErrorMessageMapper"/> — đồng bộ với dòng trạng thái trên màn hình.
/// </summary>
public static class ApiErrorUiFeedback
{
    public const int DefaultToastMaxChars = 520;

    /// <summary>
    /// Rút gọn nội dung toast (tránh vượt quá chiều rộng panel).
    /// </summary>
    public static string TruncateForToast(string message, int maxChars = DefaultToastMaxChars)
    {
        if (string.IsNullOrEmpty(message) || message.Length <= maxChars)
        {
            return message;
        }

        return message[..maxChars] + "…";
    }

    /// <summary>
    /// Toast cảnh báo cho ngoại lệ mạng/HTTP (đã map sang tiếng người dùng).
    /// </summary>
    public static Task ShowNetworkExceptionToastAsync(
        INotificationService notifications,
        Exception ex,
        CancellationToken cancellationToken = default)
    {
        string msg = NetworkErrorMessageMapper.FormatForUser(ex);
        return ShowWarningToastAsync(notifications, msg, cancellationToken);
    }

    /// <summary>
    /// Toast cảnh báo với nội dung tùy ý (lỗi API envelope, v.v.).
    /// </summary>
    public static Task ShowWarningToastAsync(
        INotificationService notifications,
        string message,
        CancellationToken cancellationToken = default)
    {
        string title = UiLanguageManager.TryLocalizeCurrent(
            "Ui_Toast_ApiErrorTitle",
            "Lỗi API hoặc mạng");
        return notifications.ShowAsync(
            title,
            TruncateForToast(message),
            NotificationDisplayKind.Warning,
            cancellationToken);
    }
}
