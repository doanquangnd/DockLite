namespace DockLite.App.Services;

/// <summary>
/// Phím tắt Ctrl+F: yêu cầu view đang mở đưa focus vào ô tìm kiếm chính (nếu có).
/// </summary>
public static class ShellPrimarySearchFocus
{
    /// <summary>
    /// View đăng ký trong Loaded, gỡ trong Unloaded.
    /// </summary>
    public static event EventHandler? Requested;

    /// <summary>
    /// Gọi từ <see cref="ViewModels.ShellViewModel"/> khi người dùng nhấn Ctrl+F.
    /// </summary>
    public static void Raise()
    {
        Requested?.Invoke(null, EventArgs.Empty);
    }
}
