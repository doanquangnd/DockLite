using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Help;
using DockLite.App.Services;
using DockLite.Contracts.Api;
using DockLite.Core.Configuration;
using DockLite.Core.Diagnostics;
using DockLite.Core.Services;
using DockLite.Infrastructure.Api;
using DockLite.Infrastructure.Wsl;

namespace DockLite.App.ViewModels;

/// <summary>
/// ViewModel vỏ ứng dụng: sidebar, trang hiện tại và dòng trạng thái service trên header.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly IDockLiteApiClient _apiClient;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly DockLiteHttpSession _httpSession;
    private readonly WslServiceHealthCache _healthCache;
    private readonly AppShellActivityState _shellActivity;
    private readonly string _appBaseDirectory;
    private readonly IAppShutdownToken _shutdownToken;

    /// <summary>
    /// Tránh vòng lặp: RefreshServiceHeaderFromApiAsync gọi SetFromHealthResponse trong khi handler Changed cũng gọi Refresh.
    /// </summary>
    private bool _suppressHealthCacheHeaderSync;

    /// <summary>Một lần toast «đang chờ health» trong phiên chờ sau spawn WSL.</summary>
    private bool _wslStartupHealthWaitToastSent;

    private readonly Lazy<ContainersViewModel> _containersLazy;
    private readonly Lazy<LogsViewModel> _logsLazy;
    private readonly Lazy<ComposeViewModel> _composeLazy;
    private readonly Lazy<ImagesViewModel> _imagesLazy;
    private readonly Lazy<NetworkVolumeViewModel> _networkVolumeLazy;
    private readonly Lazy<CleanupViewModel> _cleanupLazy;
    private readonly Lazy<AppDebugLogViewModel> _appDebugLogLazy;

    public ShellViewModel(
        DashboardViewModel dashboard,
        Lazy<ContainersViewModel> containers,
        Lazy<LogsViewModel> logs,
        Lazy<ComposeViewModel> compose,
        Lazy<ImagesViewModel> images,
        Lazy<NetworkVolumeViewModel> networkVolume,
        Lazy<CleanupViewModel> cleanup,
        SettingsViewModel settings,
        Lazy<AppDebugLogViewModel> appDebugLog,
        IDockLiteApiClient apiClient,
        IDialogService dialogService,
        INotificationService notificationService,
        DockLiteHttpSession httpSession,
        WslServiceHealthCache healthCache,
        AppShellActivityState shellActivity,
        string appBaseDirectory,
        IAppShutdownToken shutdownToken)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _httpSession = httpSession;
        _healthCache = healthCache;
        _shellActivity = shellActivity;
        _appBaseDirectory = appBaseDirectory;
        _shutdownToken = shutdownToken;
        _containersLazy = containers;
        _logsLazy = logs;
        _composeLazy = compose;
        _imagesLazy = images;
        _networkVolumeLazy = networkVolume;
        _cleanupLazy = cleanup;
        _appDebugLogLazy = appDebugLog;
        _healthCache.Changed += OnHealthCacheChanged;
        Dashboard = dashboard;
        Settings = settings;
        CurrentPage = dashboard;
    }

    partial void OnCurrentPageChanged(object? value)
    {
        _shellActivity.SetContainersPageVisible(_containersLazy.IsValueCreated && ReferenceEquals(value, _containersLazy.Value));
        _shellActivity.SetDashboardPageVisible(ReferenceEquals(value, Dashboard));
        _shellActivity.SetLogsPageVisible(_logsLazy.IsValueCreated && ReferenceEquals(value, _logsLazy.Value));
        _shellActivity.SetComposePageVisible(_composeLazy.IsValueCreated && ReferenceEquals(value, _composeLazy.Value));
        _shellActivity.SetImagesPageVisible(_imagesLazy.IsValueCreated && ReferenceEquals(value, _imagesLazy.Value));
        _shellActivity.SetNetworkVolumePageVisible(_networkVolumeLazy.IsValueCreated && ReferenceEquals(value, _networkVolumeLazy.Value));
        SyncNavHighlightFromCurrentPage();
    }

    /// <summary>
    /// Gán <see cref="NavHighlightKey"/> theo <see cref="CurrentPage"/> (sidebar).
    /// </summary>
    private void SyncNavHighlightFromCurrentPage()
    {
        object? p = CurrentPage;
        if (ReferenceEquals(p, Dashboard))
        {
            NavHighlightKey = "dashboard";
            return;
        }

        if (_containersLazy.IsValueCreated && ReferenceEquals(p, _containersLazy.Value))
        {
            NavHighlightKey = "containers";
            return;
        }

        if (_logsLazy.IsValueCreated && ReferenceEquals(p, _logsLazy.Value))
        {
            NavHighlightKey = "logs";
            return;
        }

        if (_composeLazy.IsValueCreated && ReferenceEquals(p, _composeLazy.Value))
        {
            NavHighlightKey = "compose";
            return;
        }

        if (_imagesLazy.IsValueCreated && ReferenceEquals(p, _imagesLazy.Value))
        {
            NavHighlightKey = "images";
            return;
        }

        if (_networkVolumeLazy.IsValueCreated && ReferenceEquals(p, _networkVolumeLazy.Value))
        {
            NavHighlightKey = "networkVolume";
            return;
        }

        if (ReferenceEquals(p, Cleanup))
        {
            NavHighlightKey = "cleanup";
            return;
        }

        if (ReferenceEquals(p, Settings))
        {
            NavHighlightKey = "settings";
            return;
        }

        if (_appDebugLogLazy.IsValueCreated && ReferenceEquals(p, _appDebugLogLazy.Value))
        {
            NavHighlightKey = "appLog";
        }
    }

    public DashboardViewModel Dashboard { get; }

    public ContainersViewModel Containers => _containersLazy.Value;

    public LogsViewModel Logs => _logsLazy.Value;

    public ComposeViewModel Compose => _composeLazy.Value;

    public ImagesViewModel Images => _imagesLazy.Value;

    public NetworkVolumeViewModel NetworkVolume => _networkVolumeLazy.Value;

    public CleanupViewModel Cleanup => _cleanupLazy.Value;

    public SettingsViewModel Settings { get; }

    public AppDebugLogViewModel AppDebugLog => _appDebugLogLazy.Value;

    [ObservableProperty]
    private string _serviceHeaderPrimaryText = "Đang khởi động…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasServiceHeaderSecondary))]
    private string _serviceHeaderSecondaryText = "";

    /// <summary>Dòng phụ (timeout) có hiển thị hay không.</summary>
    public bool HasServiceHeaderSecondary => !string.IsNullOrEmpty(ServiceHeaderSecondaryText);

    /// <summary>Tiến trình Start/Stop/Restart từ header (tách với IsBusy của từng trang).</summary>
    [ObservableProperty]
    private bool _isWslServiceCommandBusy;

    /// <summary>Thanh tiến trình khởi động WSL / chờ health (mở app).</summary>
    [ObservableProperty]
    private bool _wslStartupProgressBarVisible;

    /// <summary>Kiểm tra ban đầu hoặc gửi script — không biết phần trăm.</summary>
    [ObservableProperty]
    private bool _wslStartupProgressBarIndeterminate = true;

    /// <summary>0–100 khi chờ health có thời gian còn lại.</summary>
    [ObservableProperty]
    private double _wslStartupProgressBarValue;

    public bool CanHeaderStartWslService => !IsWslServiceCommandBusy && _healthCache.LastHealthy != true;

    public bool CanHeaderStopWslService => !IsWslServiceCommandBusy && _healthCache.LastHealthy == true;

    public bool CanHeaderRestartWslService => !IsWslServiceCommandBusy && _healthCache.LastHealthy == true;

    [ObservableProperty]
    private object? _currentPage;

    /// <summary>
    /// Khóa khớp với <c>Tag</c> nút sidebar để tô trạng thái mục đang mở.
    /// </summary>
    [ObservableProperty]
    private string _navHighlightKey = "dashboard";

    partial void OnIsWslServiceCommandBusyChanged(bool value)
    {
        NotifyHeaderWslServiceButtons();
    }

    private void NotifyHeaderWslServiceButtons()
    {
        OnPropertyChanged(nameof(CanHeaderStartWslService));
        OnPropertyChanged(nameof(CanHeaderStopWslService));
        OnPropertyChanged(nameof(CanHeaderRestartWslService));
    }

    private void OnHealthCacheChanged(object? sender, EventArgs e)
    {
        NotifyHeaderWslServiceButtons();
        if (_suppressHealthCacheHeaderSync)
        {
            return;
        }

        if (_healthCache.LastHealthy == false)
        {
            ServiceHeaderPrimaryText = "Service WSL: không phản hồi";
            ServiceHeaderSecondaryText = "";
            return;
        }

        if (_healthCache.LastHealthy == true)
        {
            _ = RefreshServiceHeaderFromApiAsync(_shutdownToken.Token);
        }
    }

    /// <summary>
    /// Cập nhật chữ header theo tiến trình tự khởi động WSL (gọi từ <see cref="IProgress{T}"/> trên UI thread).
    /// </summary>
    public void ApplyWslStartupProgress(WslStartupProgress p)
    {
        switch (p.Phase)
        {
            case WslStartupPhase.CheckingInitialHealth:
                _wslStartupHealthWaitToastSent = false;
                ServiceHeaderPrimaryText = "Đang kiểm tra kết nối tới service WSL…";
                ServiceHeaderSecondaryText = "";
                WslStartupProgressBarVisible = true;
                WslStartupProgressBarIndeterminate = true;
                WslStartupProgressBarValue = 0;
                break;
            case WslStartupPhase.LaunchingWslScript:
                ServiceHeaderPrimaryText = "Đang gửi lệnh tới WSL (restart-server.sh)…";
                ServiceHeaderSecondaryText = "";
                WslStartupProgressBarVisible = true;
                WslStartupProgressBarIndeterminate = true;
                WslStartupProgressBarValue = 0;
                break;
            case WslStartupPhase.WaitingHealthAfterWsl:
                ServiceHeaderPrimaryText = "Chờ phản hồi /api/health sau khi restart service trong WSL";
                int total = WslDockerServiceAutoStart.GetHealthWaitAfterWslSeconds(Settings.GetSettingsSnapshotForWslCommands());
                int sec = p.SecondsRemaining ?? 0;
                ServiceHeaderSecondaryText = $"Còn ~{sec}s / {total}s (timeout)";
                WslStartupProgressBarVisible = true;
                WslStartupProgressBarIndeterminate = false;
                if (total > 0)
                {
                    double elapsed = total - Math.Clamp(sec, 0, total);
                    WslStartupProgressBarValue = Math.Clamp(100.0 * elapsed / total, 0, 100);
                }
                else
                {
                    WslStartupProgressBarValue = 0;
                }

                if (!_wslStartupHealthWaitToastSent)
                {
                    _wslStartupHealthWaitToastSent = true;
                    int totalToast = total;
                    _ = ShowWslHealthWaitStartedToastAsync(totalToast);
                }

                break;
        }
    }

    /// <summary>
    /// Ẩn thanh tiến trình chờ health (sau khi đồng bộ header từ API hoặc kết thúc khởi động).
    /// </summary>
    private void ClearWslStartupProgressBar()
    {
        WslStartupProgressBarVisible = false;
        WslStartupProgressBarIndeterminate = true;
        WslStartupProgressBarValue = 0;
    }

    /// <summary>
    /// Toast một lần khi bắt đầu vòng chờ /api/health (bổ sung cho thanh tiến trình header).
    /// </summary>
    private async Task ShowWslHealthWaitStartedToastAsync(int totalSeconds)
    {
        try
        {
            await _notificationService
                .ShowAsync(
                    "DockLite — WSL",
                    $"Đang chờ phản hồi /api/health (tối đa {totalSeconds}s). Tiến trình hiển thị trên header.",
                    NotificationDisplayKind.Info,
                    _shutdownToken.Token)
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Đóng app.
        }
    }

    /// <summary>
    /// Đồng bộ dòng trạng thái với GET /api/health (sau khởi động hoặc khi cần làm mới).
    /// </summary>
    public async Task RefreshServiceHeaderFromApiAsync(CancellationToken cancellationToken = default)
    {
        ClearWslStartupProgressBar();
        ServiceHeaderSecondaryText = "";
        try
        {
            HealthResponse? health = await _apiClient.GetHealthAsync(cancellationToken).ConfigureAwait(true);
            if (health is null)
            {
                ServiceHeaderPrimaryText = "Service WSL: không phản hồi";
                _suppressHealthCacheHeaderSync = true;
                try
                {
                    _healthCache.SetFromHealthResponse(null);
                }
                finally
                {
                    _suppressHealthCacheHeaderSync = false;
                }

                return;
            }

            string v = string.IsNullOrWhiteSpace(health.Version) ? "" : $" — v{health.Version.Trim()}";
            ServiceHeaderPrimaryText = $"{health.Service}: {health.Status}{v}";
            _suppressHealthCacheHeaderSync = true;
            try
            {
                _healthCache.SetFromHealthResponse(health);
            }
            finally
            {
                _suppressHealthCacheHeaderSync = false;
            }
        }
        catch (OperationCanceledException)
        {
            // Giữ nguyên chữ hiện tại khi hủy.
        }
        catch
        {
            ServiceHeaderPrimaryText = "Service WSL: không phản hồi";
            _suppressHealthCacheHeaderSync = true;
            try
            {
                _healthCache.SetFromHealthResponse(null);
            }
            finally
            {
                _suppressHealthCacheHeaderSync = false;
            }
        }
    }

    [RelayCommand]
    private async Task HeaderStartWslServiceAsync()
    {
        IsWslServiceCommandBusy = true;
        try
        {
            AppSettings snapshot = Settings.GetSettingsSnapshotForWslCommands();
            _httpSession.Reconfigure(snapshot);
            (bool sent, bool healthOk, string msg) = await WslDockerServiceAutoStart
                .TryStartServiceManuallyAndWaitForHealthAsync(_httpSession, snapshot, _appBaseDirectory, _shutdownToken.Token)
                .ConfigureAwait(true);
            AppFileLog.Write("WSL header", msg + (sent && healthOk ? " [health OK]" : ""));
            await RefreshServiceHeaderFromApiAsync(_shutdownToken.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Đóng app.
        }
        finally
        {
            IsWslServiceCommandBusy = false;
        }
    }

    [RelayCommand]
    private async Task HeaderStopWslServiceAsync()
    {
        IsWslServiceCommandBusy = true;
        try
        {
            AppSettings snapshot = Settings.GetSettingsSnapshotForWslCommands();
            _httpSession.Reconfigure(snapshot);
            if (!WslDockerServiceAutoStart.TryStopServiceManually(snapshot, _appBaseDirectory, out string msg))
            {
                await _dialogService.ShowInfoAsync(msg, "DockLite").ConfigureAwait(true);
                await RefreshServiceHeaderFromApiAsync(_shutdownToken.Token).ConfigureAwait(true);
                return;
            }

            await Task.Delay(800, _shutdownToken.Token).ConfigureAwait(true);
            await RefreshServiceHeaderFromApiAsync(_shutdownToken.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Đóng app.
        }
        finally
        {
            IsWslServiceCommandBusy = false;
        }
    }

    [RelayCommand]
    private async Task HeaderRestartWslServiceAsync()
    {
        IsWslServiceCommandBusy = true;
        try
        {
            AppSettings snapshot = Settings.GetSettingsSnapshotForWslCommands();
            _httpSession.Reconfigure(snapshot);
            (bool sent, bool healthOk, string msg) = await WslDockerServiceAutoStart
                .TryRestartServiceManuallyAndWaitForHealthAsync(_httpSession, snapshot, _appBaseDirectory, _shutdownToken.Token)
                .ConfigureAwait(true);
            AppFileLog.Write("WSL header restart", msg + (sent && healthOk ? " [health OK]" : ""));
            await RefreshServiceHeaderFromApiAsync(_shutdownToken.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Đóng app.
        }
        finally
        {
            IsWslServiceCommandBusy = false;
        }
    }

    [RelayCommand]
    private void NavigateDashboard()
    {
        CurrentPage = Dashboard;
    }

    [RelayCommand]
    private async Task NavigateContainersAsync()
    {
        CurrentPage = Containers;
        await Containers.RefreshCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task NavigateLogsAsync()
    {
        CurrentPage = Logs;
        // Cho WPF vẽ trang Log trước khi bắt đầu gọi mạng; tải danh sách chạy nền (không chặn kết thúc lệnh điều hướng).
        await Task.Yield();
        _ = LoadLogsContainersAfterNavigateAsync();
    }

    /// <summary>
    /// Tải danh sách container cho trang Log sau khi đã đổi trang (tránh xếp hàng sau một tác vụ mạng dài trên cùng một chuỗi điều hướng).
    /// </summary>
    private async Task LoadLogsContainersAfterNavigateAsync()
    {
        try
        {
            await Logs.LoadContainersCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppFileLog.Write("NavigateLogs", ex.ToString());
        }
    }

    [RelayCommand]
    private async Task NavigateComposeAsync()
    {
        CurrentPage = Compose;
        await Compose.LoadProjectsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task NavigateImagesAsync()
    {
        CurrentPage = Images;
        await Images.RefreshCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task NavigateNetworkVolumeAsync()
    {
        CurrentPage = NetworkVolume;
        await NetworkVolume.RefreshCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void NavigateCleanup()
    {
        CurrentPage = Cleanup;
    }

    [RelayCommand]
    private void NavigateSettings()
    {
        CurrentPage = Settings;
    }

    [RelayCommand]
    private void NavigateAppDebugLog()
    {
        CurrentPage = AppDebugLog;
        AppDebugLog.LoadRecent();
    }

    /// <summary>
    /// Hiển thị hộp thoại trợ giúp theo màn hình đang mở (CurrentPage).
    /// </summary>
    [RelayCommand]
    private async Task ShowHelpAsync()
    {
        (string shortTitle, string body) = PageHelpTexts.GetForCurrentPage(CurrentPage);
        string titlePrefix = UiLanguageManager.TryLocalize(Application.Current, "Ui_Help_DialogTitlePrefix", "Trợ giúp — ");
        await _dialogService.ShowInfoAsync(body, titlePrefix + shortTitle).ConfigureAwait(true);
    }
}
