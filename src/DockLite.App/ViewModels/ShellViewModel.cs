using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Help;
using DockLite.App.Services;
using DockLite.Contracts.Api;
using DockLite.Core.Configuration;
using DockLite.Core.Diagnostics;
using DockLite.Infrastructure.Api;
using DockLite.Infrastructure.Wsl;

namespace DockLite.App.ViewModels;

/// <summary>
/// ViewModel vỏ ứng dụng: sidebar, trang hiện tại và dòng trạng thái service trên header.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private static readonly SolidColorBrush SidebarConnectionOkBrush = FreezeBrush(34, 197, 94);
    private static readonly SolidColorBrush SidebarConnectionOfflineBrush = FreezeBrush(239, 68, 68);
    private static readonly SolidColorBrush SidebarConnectionUnknownBrush = FreezeBrush(148, 163, 184);

    private static SolidColorBrush FreezeBrush(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    private readonly ISystemDiagnosticsScreenApi _systemDiagnosticsApi;
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
    private readonly Lazy<DockerEventsViewModel> _dockerEventsLazy;
    private readonly Lazy<AppDebugLogViewModel> _appDebugLogLazy;

    public ShellViewModel(
        DashboardViewModel dashboard,
        Lazy<ContainersViewModel> containers,
        Lazy<LogsViewModel> logs,
        Lazy<ComposeViewModel> compose,
        Lazy<ImagesViewModel> images,
        Lazy<NetworkVolumeViewModel> networkVolume,
        Lazy<CleanupViewModel> cleanup,
        Lazy<DockerEventsViewModel> dockerEvents,
        SettingsViewModel settings,
        Lazy<AppDebugLogViewModel> appDebugLog,
        ISystemDiagnosticsScreenApi systemDiagnosticsApi,
        IDialogService dialogService,
        INotificationService notificationService,
        DockLiteHttpSession httpSession,
        WslServiceHealthCache healthCache,
        AppShellActivityState shellActivity,
        string appBaseDirectory,
        IAppShutdownToken shutdownToken)
    {
        _systemDiagnosticsApi = systemDiagnosticsApi;
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
        _dockerEventsLazy = dockerEvents;
        _appDebugLogLazy = appDebugLog;
        _healthCache.Changed += OnHealthCacheChanged;
        Dashboard = dashboard;
        Settings = settings;
        CurrentPage = dashboard;
        UpdateSidebarConnectionIndicator();
    }

    partial void OnCurrentPageChanged(object? value)
    {
        _shellActivity.SetContainersPageVisible(_containersLazy.IsValueCreated && ReferenceEquals(value, _containersLazy.Value));
        _shellActivity.SetDashboardPageVisible(ReferenceEquals(value, Dashboard));
        _shellActivity.SetLogsPageVisible(_logsLazy.IsValueCreated && ReferenceEquals(value, _logsLazy.Value));
        _shellActivity.SetComposePageVisible(_composeLazy.IsValueCreated && ReferenceEquals(value, _composeLazy.Value));
        _shellActivity.SetImagesPageVisible(_imagesLazy.IsValueCreated && ReferenceEquals(value, _imagesLazy.Value));
        _shellActivity.SetNetworkVolumePageVisible(_networkVolumeLazy.IsValueCreated && ReferenceEquals(value, _networkVolumeLazy.Value));
        _shellActivity.SetDockerEventsPageVisible(_dockerEventsLazy.IsValueCreated && ReferenceEquals(value, _dockerEventsLazy.Value));
        SyncNavHighlightFromCurrentPage();
        if (ReferenceEquals(value, Containers))
        {
            Containers.RefreshStatsAlertSettingsFromStore();
        }
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

        if (_dockerEventsLazy.IsValueCreated && ReferenceEquals(p, _dockerEventsLazy.Value))
        {
            NavHighlightKey = "dockerEvents";
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

    private static string LocalizedShellServiceHeaderNoResponse() =>
        UiLanguageManager.TryLocalizeCurrent(
            "Ui_Shell_ServiceHeader_NoResponse",
            "Service WSL: không phản hồi");

    private void UpdateSidebarConnectionIndicator()
    {
        bool? h = _healthCache.LastHealthy;
        SidebarConnectionDotFill = h switch
        {
            true => SidebarConnectionOkBrush,
            false => SidebarConnectionOfflineBrush,
            _ => SidebarConnectionUnknownBrush,
        };
    }

    public DashboardViewModel Dashboard { get; }

    public ContainersViewModel Containers => _containersLazy.Value;

    public LogsViewModel Logs => _logsLazy.Value;

    public ComposeViewModel Compose => _composeLazy.Value;

    public ImagesViewModel Images => _imagesLazy.Value;

    public NetworkVolumeViewModel NetworkVolume => _networkVolumeLazy.Value;

    public CleanupViewModel Cleanup => _cleanupLazy.Value;

    public DockerEventsViewModel DockerEvents => _dockerEventsLazy.Value;

    public SettingsViewModel Settings { get; }

    public AppDebugLogViewModel AppDebugLog => _appDebugLogLazy.Value;

    [ObservableProperty]
    private string _serviceHeaderPrimaryText = UiLanguageManager.TryLocalizeCurrent(
        "Ui_Shell_ServiceHeader_Starting",
        "Đang khởi động…");

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

    /// <summary>
    /// Hiển thị banner khi cache health báo không kết nối được tới service (thay vì chỉ dựa vào lỗi rải rác trên từng nút).
    /// </summary>
    public bool ShowServiceDisconnectedBanner => _healthCache.LastHealthy == false;

    [ObservableProperty]
    private object? _currentPage;

    /// <summary>
    /// Khóa khớp với <c>Tag</c> nút sidebar để tô trạng thái mục đang mở.
    /// </summary>
    [ObservableProperty]
    private string _navHighlightKey = "dashboard";

    /// <summary>
    /// Chấm tròn cạnh tiêu đề sidebar: xanh (health OK), đỏ (mất kết nối), xám (chưa biết).
    /// </summary>
    [ObservableProperty]
    private Brush _sidebarConnectionDotFill = SidebarConnectionUnknownBrush;

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
        UpdateSidebarConnectionIndicator();
        OnPropertyChanged(nameof(ShowServiceDisconnectedBanner));
        if (_suppressHealthCacheHeaderSync)
        {
            return;
        }

        if (_healthCache.LastHealthy == false)
        {
            ServiceHeaderPrimaryText = LocalizedShellServiceHeaderNoResponse();
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
                ServiceHeaderPrimaryText = UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Shell_WslStartup_CheckingHealth",
                    "Đang kiểm tra kết nối tới service WSL…");
                ServiceHeaderSecondaryText = "";
                WslStartupProgressBarVisible = true;
                WslStartupProgressBarIndeterminate = true;
                WslStartupProgressBarValue = 0;
                break;
            case WslStartupPhase.LaunchingWslScript:
                ServiceHeaderPrimaryText = UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Shell_WslStartup_LaunchingScript",
                    "Đang gửi lệnh tới WSL (restart-server.sh)…");
                ServiceHeaderSecondaryText = "";
                WslStartupProgressBarVisible = true;
                WslStartupProgressBarIndeterminate = true;
                WslStartupProgressBarValue = 0;
                break;
            case WslStartupPhase.WaitingHealthAfterWsl:
                ServiceHeaderPrimaryText = UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Shell_WslStartup_WaitingHealthAfterRestart",
                    "Chờ phản hồi /api/health sau khi restart service trong WSL");
                int total = WslDockerServiceAutoStart.GetHealthWaitAfterWslSeconds(Settings.GetSettingsSnapshotForWslCommands());
                int sec = p.SecondsRemaining ?? 0;
                ServiceHeaderSecondaryText = UiLanguageManager.TryLocalizeFormatCurrent(
                    "Ui_Shell_Header_HealthWaitSecondaryFormat",
                    "Còn ~{0}s / {1}s (timeout)",
                    sec,
                    total);
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
                    UiLanguageManager.TryLocalizeCurrent(
                        "Ui_Shell_Toast_HealthWaitTitle",
                        "DockLite — WSL"),
                    UiLanguageManager.TryLocalizeFormatCurrent(
                        "Ui_Shell_Toast_HealthWaitBodyFormat",
                        "Đang chờ phản hồi /api/health (tối đa {0}s). Tiến trình hiển thị trên header.",
                        totalSeconds),
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
    /// Thử lại kết nối tới service (từ banner «mất kết nối»).
    /// </summary>
    [RelayCommand]
    private async Task RetryServiceConnection()
    {
        await RefreshServiceHeaderFromApiAsync(_shutdownToken.Token).ConfigureAwait(true);
    }

    /// <summary>
    /// Đồng bộ dòng trạng thái với GET /api/health và GET /api/docker/info (cùng tiêu chí với trang Tổng quan).
    /// </summary>
    public async Task RefreshServiceHeaderFromApiAsync(CancellationToken cancellationToken = default)
    {
        ClearWslStartupProgressBar();
        ServiceHeaderSecondaryText = "";
        try
        {
            Task<HealthResponse?> healthTask = _systemDiagnosticsApi.GetHealthAsync(cancellationToken);
            Task<ApiResult<DockerInfoData>> dockerTask = _systemDiagnosticsApi.GetDockerInfoAsync(cancellationToken);
            await Task.WhenAll(healthTask, dockerTask).ConfigureAwait(true);

            HealthResponse? health = await healthTask.ConfigureAwait(true);
            ApiResult<DockerInfoData> docker = await dockerTask.ConfigureAwait(true);

            bool connectivityOk = health is not null && docker.Success && docker.Data is not null;
            if (!connectivityOk)
            {
                if (health is null)
                {
                    ServiceHeaderPrimaryText = LocalizedShellServiceHeaderNoResponse();
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

                string v = string.IsNullOrWhiteSpace(health.Version)
                    ? ""
                    : UiLanguageManager.TryLocalizeFormatCurrent(
                        "Ui_Shell_ServiceHeader_VersionSuffixFormat",
                        " — v{0}",
                        health.Version.Trim());
                _suppressHealthCacheHeaderSync = true;
                try
                {
                    ServiceHeaderPrimaryText = UiLanguageManager.TryLocalizeFormatCurrent(
                        "Ui_Shell_ServiceHeader_HealthOkDockerDownFormat",
                        "{0}: {1}{2} — Docker: không kết nối",
                        health.Service,
                        health.Status,
                        v);
                    _healthCache.SetFromHealthResponse(null);
                }
                finally
                {
                    _suppressHealthCacheHeaderSync = false;
                }

                return;
            }

            HealthResponse healthOk = health!;
            string vOk = string.IsNullOrWhiteSpace(healthOk.Version)
                ? ""
                : UiLanguageManager.TryLocalizeFormatCurrent(
                    "Ui_Shell_ServiceHeader_VersionSuffixFormat",
                    " — v{0}",
                    healthOk.Version.Trim());
            ServiceHeaderPrimaryText = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Shell_ServiceHeader_HealthLineFormat",
                "{0}: {1}{2}",
                healthOk.Service,
                healthOk.Status,
                vOk);
            _suppressHealthCacheHeaderSync = true;
            try
            {
                _healthCache.SetFromHealthResponse(healthOk);
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
            ServiceHeaderPrimaryText = LocalizedShellServiceHeaderNoResponse();
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
            DiagnosticTelemetry.WriteManualWslLifecycle(snapshot, "header", "start", sent, healthOk);
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
                DiagnosticTelemetry.WriteManualWslLifecycle(snapshot, "header", "stop", false);
                await _dialogService
                    .ShowInfoAsync(
                        msg,
                        UiLanguageManager.TryLocalizeCurrent("Ui_MainWindow_Title", "DockLite"))
                    .ConfigureAwait(true);
                await RefreshServiceHeaderFromApiAsync(_shutdownToken.Token).ConfigureAwait(true);
                return;
            }

            DiagnosticTelemetry.WriteManualWslLifecycle(snapshot, "header", "stop", true);
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
            DiagnosticTelemetry.WriteManualWslLifecycle(snapshot, "header", "restart", sent, healthOk);
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
    private void NavigateDockerEvents()
    {
        CurrentPage = DockerEvents;
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

    /// <summary>
    /// Từ banner mất kết nối: mở Cài đặt tab Kết nối (checklist + kiểm tra nhanh).
    /// </summary>
    [RelayCommand]
    private void OpenSettingsConnectionFromBanner()
    {
        Settings.SelectedTabIndex = 0;
        CurrentPage = Settings;
    }

    [RelayCommand]
    private void NavigateAppDebugLog()
    {
        CurrentPage = AppDebugLog;
        AppDebugLog.LoadRecent();
    }

    /// <summary>
    /// Làm mới dữ liệu trang hiện tại (F5).
    /// </summary>
    [RelayCommand]
    private async Task RefreshCurrentPageAsync()
    {
        object? p = CurrentPage;
        if (p is null)
        {
            return;
        }

        if (ReferenceEquals(p, Dashboard))
        {
            await Dashboard.RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);
            return;
        }

        if (_containersLazy.IsValueCreated && ReferenceEquals(p, _containersLazy.Value))
        {
            await Containers.RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);
            return;
        }

        if (_logsLazy.IsValueCreated && ReferenceEquals(p, _logsLazy.Value))
        {
            await Logs.LoadContainersCommand.ExecuteAsync(null).ConfigureAwait(true);
            return;
        }

        if (_composeLazy.IsValueCreated && ReferenceEquals(p, _composeLazy.Value))
        {
            await Compose.LoadProjectsCommand.ExecuteAsync(null).ConfigureAwait(true);
            return;
        }

        if (_imagesLazy.IsValueCreated && ReferenceEquals(p, _imagesLazy.Value))
        {
            await Images.RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);
            return;
        }

        if (_networkVolumeLazy.IsValueCreated && ReferenceEquals(p, _networkVolumeLazy.Value))
        {
            await NetworkVolume.RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);
            return;
        }

        if (ReferenceEquals(p, Settings))
        {
            return;
        }

        if (ReferenceEquals(p, Cleanup))
        {
            return;
        }

        if (_dockerEventsLazy.IsValueCreated && ReferenceEquals(p, _dockerEventsLazy.Value))
        {
            return;
        }

        if (_appDebugLogLazy.IsValueCreated && ReferenceEquals(p, _appDebugLogLazy.Value))
        {
            AppDebugLog.RefreshCommand.Execute(null);
        }
    }

    /// <summary>
    /// Hiển thị hộp thoại trợ giúp theo màn hình đang mở (CurrentPage).
    /// </summary>
    [RelayCommand]
    private async Task ShowHelpAsync()
    {
        (string shortTitle, string body) = PageHelpTexts.GetForCurrentPage(CurrentPage);
        string titlePrefix = UiLanguageManager.TryLocalize(Application.Current, "Ui_Help_DialogTitlePrefix", "Trợ giúp — ");
        Uri? apiBase = _httpSession.Client.BaseAddress;
        string? lanSecurityMarkdownPath = LanSecurityDocPaths.TryResolve(_appBaseDirectory);
        IReadOnlyList<HelpHyperlink> links = PageHelpTexts.GetHelpLinksForPage(CurrentPage, apiBase, lanSecurityMarkdownPath);
        await _dialogService
            .ShowHelpAsync(body, titlePrefix + shortTitle, links.Count > 0 ? links : null)
            .ConfigureAwait(true);
    }

    /// <summary>
    /// Ctrl+1 … Ctrl+9: điều hướng sidebar theo thứ tự (Tổng quan → … → Cài đặt).
    /// </summary>
    [RelayCommand]
    private async Task NavigateSidebarByIndexAsync(object? parameter)
    {
        int idx = ParseSidebarIndex(parameter);
        if (idx < 1 || idx > 10)
        {
            return;
        }

        switch (idx)
        {
            case 1:
                NavigateDashboard();
                return;
            case 2:
                await NavigateContainersAsync();
                return;
            case 3:
                await NavigateLogsAsync();
                return;
            case 4:
                await NavigateComposeAsync();
                return;
            case 5:
                await NavigateImagesAsync();
                return;
            case 6:
                await NavigateNetworkVolumeAsync();
                return;
            case 7:
                NavigateCleanup();
                return;
            case 8:
                NavigateDockerEvents();
                return;
            case 9:
                NavigateAppDebugLog();
                return;
            case 10:
                NavigateSettings();
                return;
        }
    }

    private static int ParseSidebarIndex(object? parameter)
    {
        return parameter switch
        {
            int i => i,
            string s when int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int n) => n,
            _ => 0,
        };
    }

    /// <summary>
    /// Ctrl+F: focus ô tìm trên trang Container / Image (nếu đang mở).
    /// </summary>
    [RelayCommand]
    private void FocusPrimarySearch()
    {
        ShellPrimarySearchFocus.Raise();
    }
}
