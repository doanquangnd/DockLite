namespace DockLite.App.Services;

/// <summary>
/// Trạng thái cửa sổ chính và tab hiện tại — dùng tạm dừng polling stats container khi không cần (tab khác, cửa sổ không active, thu nhỏ).
/// </summary>
public sealed class AppShellActivityState
{
    private bool _mainWindowInteractive = true;
    private bool _containersPageVisible;
    private bool _dashboardPageVisible;
    private bool _logsPageVisible;
    private bool _composePageVisible;
    private bool _imagesPageVisible;
    private bool _networkVolumePageVisible;

    /// <summary>Cửa sổ không thu nhỏ và đang foreground (hoặc tương tác).</summary>
    public bool IsMainWindowInteractive => _mainWindowInteractive;

    /// <summary>Đang mở tab Log container.</summary>
    public bool IsLogsPageVisible => _logsPageVisible;

    /// <summary>
    /// Có nên chạy timer / gọi API làm mới stats realtime hay không.
    /// </summary>
    public bool ShouldPollContainerStats => _mainWindowInteractive && _containersPageVisible;

    /// <summary>
    /// Làm mới định kỳ trang Tổng quan (health + Docker info) khi tab đang mở và cửa sổ tương tác.
    /// </summary>
    public bool ShouldAutoRefreshDashboard => _mainWindowInteractive && _dashboardPageVisible;

    /// <summary>
    /// Timer gộp chunk log (follow WebSocket) chỉ chạy khi tab Log đang mở và cửa sổ tương tác — tránh tải UI khi chuyển tab hoặc sang app khác.
    /// </summary>
    public bool ShouldProcessLogsFollowFlush => _mainWindowInteractive && _logsPageVisible;

    /// <summary>
    /// Có nên gọi API tải danh sách container (tab Container) không — chỉ khi tab đang mở và cửa sổ tương tác.
    /// </summary>
    public bool ShouldRefreshContainerList => _mainWindowInteractive && _containersPageVisible;

    /// <summary>
    /// Có nên gọi API tải danh sách image (tab Image) không.
    /// </summary>
    public bool ShouldRefreshImageList => _mainWindowInteractive && _imagesPageVisible;

    /// <summary>
    /// Có nên gọi API tải danh sách container cho ComboBox (tab Log) không.
    /// </summary>
    public bool ShouldRefreshLogsContainerList => _mainWindowInteractive && _logsPageVisible;

    /// <summary>
    /// Có nên gọi API tải danh sách project Compose (tab Compose) không.
    /// </summary>
    public bool ShouldRefreshComposeProjectList => _mainWindowInteractive && _composePageVisible;

    /// <summary>
    /// Có nên gọi API tải mạng và volume (tab Mạng và volume) không.
    /// </summary>
    public bool ShouldRefreshNetworkVolumeList => _mainWindowInteractive && _networkVolumePageVisible;

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

    /// <summary>
    /// Sidebar đang mở trang Log / container log.
    /// </summary>
    public void SetLogsPageVisible(bool visible)
    {
        if (_logsPageVisible == visible)
        {
            return;
        }

        _logsPageVisible = visible;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Sidebar đang mở trang Compose (dùng khi sau này có timer/đồng bộ nền).
    /// </summary>
    public void SetComposePageVisible(bool visible)
    {
        if (_composePageVisible == visible)
        {
            return;
        }

        _composePageVisible = visible;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Sidebar đang mở trang Image.
    /// </summary>
    public void SetImagesPageVisible(bool visible)
    {
        if (_imagesPageVisible == visible)
        {
            return;
        }

        _imagesPageVisible = visible;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Sidebar đang mở trang Mạng và volume.
    /// </summary>
    public void SetNetworkVolumePageVisible(bool visible)
    {
        if (_networkVolumePageVisible == visible)
        {
            return;
        }

        _networkVolumePageVisible = visible;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
