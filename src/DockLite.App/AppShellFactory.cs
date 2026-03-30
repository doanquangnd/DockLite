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

    public AppShellFactory(
        IDialogService dialogService,
        INotificationService notificationService,
        IAppShutdownToken shutdownToken)
    {
        _dialogService = dialogService;
        _notificationService = notificationService;
        _shutdownToken = shutdownToken;
    }

    /// <summary>
    /// Khởi tạo ViewModel gốc và trả về session HTTP cùng snapshot cài đặt đã load.
    /// </summary>
    public ShellCompositionResult Create(string appBaseDirectory)
    {
        var store = new AppSettingsStore();
        AppSettings loaded = store.Load();

        var httpSession = new DockLiteHttpSession(loaded);

        IDockLiteApiClient apiClient = new DockLiteApiClient(httpSession);
        ILogStreamClient logStream = new LogStreamClient(httpSession);
        IDialogService dialogService = _dialogService;
        var dashboardVm = new DashboardViewModel(apiClient, _notificationService);
        var containersVm = new ContainersViewModel(apiClient, dialogService, _shutdownToken);
        var logsVm = new LogsViewModel(apiClient, logStream, _shutdownToken);
        var composeVm = new ComposeViewModel(apiClient, _notificationService, _shutdownToken);
        var imagesVm = new ImagesViewModel(apiClient, dialogService, _notificationService, _shutdownToken);
        var cleanupVm = new CleanupViewModel(apiClient, dialogService, _shutdownToken);
        var settingsVm = new SettingsViewModel(store, httpSession, apiClient, appBaseDirectory, loaded, _shutdownToken);
        var appDebugLogVm = new AppDebugLogViewModel();
        var shellVm = new ShellViewModel(
            dashboardVm,
            containersVm,
            logsVm,
            composeVm,
            imagesVm,
            cleanupVm,
            settingsVm,
            appDebugLogVm);

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
