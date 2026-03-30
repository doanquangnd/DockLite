namespace DockLite.App.Services;

/// <summary>
/// Token hủy khi ứng dụng đóng cửa sổ chính (dừng các tác vụ bất đồng bộ dài).
/// </summary>
public interface IAppShutdownToken
{
    CancellationToken Token { get; }

    /// <summary>
    /// Báo hiệu hủy (gọi khi đóng MainWindow).
    /// </summary>
    void Cancel();
}
