using System.Windows;
using DockLite.App.Services;
using DockLite.App.ViewModels;
using DockLite.Core.Configuration;
using DockLite.Core.Diagnostics;
using DockLite.Infrastructure.Configuration;
using DockLite.Core.Security;
using DockLite.Core.Services;
using DockLite.Infrastructure.Api;

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
        IServiceApiTokenStore apiTokenStore = new WindowsServiceApiTokenStore();
        ITrustedFingerprintStore tlsPinStore = new WindowsTrustedFingerprintStore();
        var store = new AppSettingsStore(apiTokenStore);
        AppSettings loaded = store.Load();
        DiagnosticTelemetry.SetEnabled(loaded.DiagnosticLocalTelemetryEnabled);
        _uiDisplay.Apply(loaded);
        if (Application.Current is not null)
        {
            ThemeManager.Apply(Application.Current, loaded);
            ThemeManager.RegisterSystemThemeListener(Application.Current, store);
            UiLanguageManager.Apply(Application.Current, loaded);
        }

        var httpSession = new DockLiteHttpSession(loaded, tlsPinStore);

        IDockLiteApiClient apiClient = new DockLiteApiClient(httpSession);
        IContainerScreenApi containerScreenApi = new ContainerScreenApi(apiClient);
        IImageScreenApi imageScreenApi = new ImageScreenApi(apiClient);
        ISystemDiagnosticsScreenApi systemDiagnosticsApi = new SystemDiagnosticsScreenApi(apiClient);
        ILogsScreenApi logsScreenApi = new LogsScreenApi(apiClient);
        ICleanupScreenApi cleanupScreenApi = new CleanupScreenApi(apiClient);
        INetworkVolumeScreenApi networkVolumeScreenApi = new NetworkVolumeScreenApi(apiClient);
        IComposeScreenApi composeScreenApi = new ComposeScreenApi(apiClient);
        ILogStreamClient logStream = new LogStreamClient(httpSession);
        IStatsStreamClient statsStream = new StatsStreamClient(httpSession);
        IDialogService dialogService = _dialogService;
        var healthCache = new WslServiceHealthCache();
        _shellActivity.AttachHealthCache(healthCache);
        var dashboardVm = new DashboardViewModel(systemDiagnosticsApi, _notificationService, _shellActivity, _shutdownToken, healthCache);
        var settingsVm = new SettingsViewModel(
            store,
            httpSession,
            systemDiagnosticsApi,
            apiClient,
            appBaseDirectory,
            loaded,
            _shutdownToken,
            healthCache,
            _uiDisplay,
            _notificationService,
            tlsPinStore,
            _dialogService);
        var containersLazy = new Lazy<ContainersViewModel>(() =>
            new ContainersViewModel(
                containerScreenApi,
                dialogService,
                _notificationService,
                _shutdownToken,
                _shellActivity,
                statsStream,
                store));
        var logsLazy = new Lazy<LogsViewModel>(() =>
            new LogsViewModel(logsScreenApi, logStream, _notificationService, _shutdownToken, _shellActivity));
        var composeLazy = new Lazy<ComposeViewModel>(() =>
            new ComposeViewModel(composeScreenApi, _notificationService, _shutdownToken, _shellActivity, loaded.WslDistribution));
        var imagesLazy = new Lazy<ImagesViewModel>(() =>
            new ImagesViewModel(imageScreenApi, dialogService, _notificationService, _shutdownToken, _shellActivity));
        var networkVolumeLazy = new Lazy<NetworkVolumeViewModel>(() =>
            new NetworkVolumeViewModel(networkVolumeScreenApi, dialogService, _notificationService, _shutdownToken, _shellActivity));
        var cleanupLazy = new Lazy<CleanupViewModel>(() =>
            new CleanupViewModel(cleanupScreenApi, dialogService, _notificationService, _shutdownToken, healthCache));
        var dockerEventsLazy = new Lazy<DockerEventsViewModel>(() =>
            new DockerEventsViewModel(apiClient, _shutdownToken));
        var appDebugLogLazy = new Lazy<AppDebugLogViewModel>(() => new AppDebugLogViewModel(_uiDisplay));
        var shellVm = new ShellViewModel(
            dashboardVm,
            containersLazy,
            logsLazy,
            composeLazy,
            imagesLazy,
            networkVolumeLazy,
            cleanupLazy,
            dockerEventsLazy,
            settingsVm,
            appDebugLogLazy,
            systemDiagnosticsApi,
            dialogService,
            _notificationService,
            httpSession,
            healthCache,
            _shellActivity,
            appBaseDirectory,
            _shutdownToken);

        return new ShellCompositionResult(shellVm, httpSession, loaded, healthCache);
    }
}

/// <summary>
/// Kết quả compose shell: ViewModel gốc, session HTTP, cài đặt đã đọc một lần và cache health.
/// </summary>
public sealed record ShellCompositionResult(
    ShellViewModel Shell,
    DockLiteHttpSession HttpSession,
    AppSettings Settings,
    WslServiceHealthCache HealthCache);
