namespace DockLite.App.Services;

/// <summary>
/// Trạng thái cửa sổ chính và tab hiện tại — dùng tạm dừng polling stats container khi không cần (tab khác, cửa sổ không active, thu nhỏ).
/// </summary>
public sealed class AppShellActivityState
{
    private bool _mainWindowInteractive = true;
    private bool _containersPageVisible;
    private bool _dashboardPageVisible;

    /// <summary>
    /// Có nên chạy timer / gọi API làm mới stats realtime hay không.
    /// </summary>
    public bool ShouldPollContainerStats => _mainWindowInteractive && _containersPageVisible;

    /// <summary>
    /// Làm mới định kỳ trang Tổng quan (health + Docker info) khi tab đang mở và cửa sổ tương tác.
    /// </summary>
    public bool ShouldAutoRefreshDashboard => _mainWindowInteractive && _dashboardPageVisible;

    public event EventHandler? Changed;

    /// <summary>
    /// Cửa sổ không thu nhỏ và đang là cửa sổ foreground (người dùng không chuyển sang app khác).
    /// </summary>
    public void SetMainWindowInteractive(bool interactive)
    {
        if (_mainWindowInteractive == interactive)
        {
            return;
        }

        _mainWindowInteractive = interactive;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Sidebar đang mở trang Container.
    /// </summary>
    public void SetContainersPageVisible(bool visible)
    {
        if (_containersPageVisible == visible)
        {
            return;
        }

        _containersPageVisible = visible;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Sidebar đang mở trang Tổng quan.
    /// </summary>
    public void SetDashboardPageVisible(bool visible)
    {
        if (_dashboardPageVisible == visible)
        {
            return;
        }

        _dashboardPageVisible = visible;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
