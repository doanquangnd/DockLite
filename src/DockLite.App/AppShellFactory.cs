using System.Windows;
using DockLite.App.Services;
using DockLite.App.ViewModels;
using DockLite.Core.Configuration;
using DockLite.Core.Services;
using DockLite.Infrastructure.Api;
using DockLite.Infrastructure.Configuration;

namespace DockLite.App;

/// <summary>
/// Tạo shell và dependency chung một lần (một lần đọc cài đặt từ store).
/// </summary>
public sealed class AppShellFactory : IAppShellFactory
{
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly IAppShutdownToken _shutdownToken;
    private readonly AppUiDisplaySettings _uiDisplay;
    private readonly AppShellActivityState _shellActivity;

    public AppShellFactory(
        IDialogService dialogService,
        INotificationService notificationService,
        IAppShutdownToken shutdownToken,
        AppUiDisplaySettings uiDisplay,
        AppShellActivityState shellActivity)
    {
        _dialogService = dialogService;
        _notificationService = notificationService;
        _shutdownToken = shutdownToken;
        _uiDisplay = uiDisplay;
        _shellActivity = shellActivity;
    }

    /// <summary>
    /// Khởi tạo ViewModel gốc và trả về session HTTP cùng snapshot cài đặt đã load.
    /// </summary>
    public ShellCompositionResult Create(string appBaseDirectory)
    {
        var store = new AppSettingsStore();
        AppSettings loaded = store.Load();
        _uiDisplay.Apply(loaded);
        if (Application.Current is not null)
        {
            ThemeManager.Apply(Application.Current, loaded);
            UiLanguageManager.Apply(Application.Current, loaded);
        }

        var httpSession = new DockLiteHttpSession(loaded);

        IDockLiteApiClient apiClient = new DockLiteApiClient(httpSession);
        ILogStreamClient logStream = new LogStreamClient(httpSession);
        IStatsStreamClient statsStream = new StatsStreamClient(httpSession);
        IDialogService dialogService = _dialogService;
        var healthCache = new WslServiceHealthCache();
        var dashboardVm = new DashboardViewModel(apiClient, _notificationService, _shellActivity, _shutdownToken);
        var settingsVm = new SettingsViewModel(store, httpSession, apiClient, appBaseDirectory, loaded, _shutdownToken, healthCache, _uiDisplay, _notificationService);
        var containersLazy = new Lazy<ContainersViewModel>(() =>
            new ContainersViewModel(apiClient, dialogService, _shutdownToken, _shellActivity, statsStream));
        var logsLazy = new Lazy<LogsViewModel>(() =>
            new LogsViewModel(apiClient, logStream, _shutdownToken, _shellActivity));
        var composeLazy = new Lazy<ComposeViewModel>(() =>
            new ComposeViewModel(apiClient, _notificationService, _shutdownToken, loaded.WslDistribution));
        var imagesLazy = new Lazy<ImagesViewModel>(() =>
            new ImagesViewModel(apiClient, dialogService, _notificationService, _shutdownToken));
        var networkVolumeLazy = new Lazy<NetworkVolumeViewModel>(() =>
            new NetworkVolumeViewModel(apiClient, _shutdownToken));
        var cleanupLazy = new Lazy<CleanupViewModel>(() =>
            new CleanupViewModel(apiClient, dialogService, _shutdownToken));
        var appDebugLogLazy = new Lazy<AppDebugLogViewModel>(() => new AppDebugLogViewModel(_uiDisplay));
        var shellVm = new ShellViewModel(
            dashboardVm,
            containersLazy,
            logsLazy,
            composeLazy,
            imagesLazy,
            networkVolumeLazy,
            cleanupLazy,
            settingsVm,
            appDebugLogLazy,
            apiClient,
            dialogService,
            _notificationService,
            httpSession,
            healthCache,
            _shellActivity,
            appBaseDirectory,
            _shutdownToken);

        return new ShellCompositionResult(shellVm, httpSession, loaded);
    }
}

/// <summary>
/// Kết quả compose shell: ViewModel gốc, session HTTP và cài đặt đã đọc một lần.
/// </summary>
public sealed record ShellCompositionResult(
    ShellViewModel Shell,
    DockLiteHttpSession HttpSession,
    AppSettings Settings);
