namespace DockLite.App.Services;

/// <summary>
/// Thông báo ngắn không chặn luồng (khác <see cref="IDialogService"/> modal).
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Hiển thị thông tin (toast); không chặn tương tác với cửa sổ chính.
    /// </summary>
    Task ShowInfoAsync(string title, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hiển thị toast theo kiểu (cảnh báo / thành công dùng màu khác thông tin).
    /// </summary>
    Task ShowAsync(
        string title,
        string message,
        NotificationDisplayKind kind = NotificationDisplayKind.Info,
        CancellationToken cancellationToken = default);
}
