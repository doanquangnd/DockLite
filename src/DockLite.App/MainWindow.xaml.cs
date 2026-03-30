using System.Windows;
using DockLite.App.ViewModels;
using DockLite.Core.Configuration;
using DockLite.Core.Services;
using DockLite.Infrastructure.Api;
using DockLite.Infrastructure.Configuration;
using DockLite.Infrastructure.Wsl;

namespace DockLite.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var store = new AppSettingsStore();
        var loaded = store.Load();

        var httpSession = new DockLiteHttpSession(loaded);

        IDockLiteApiClient apiClient = new DockLiteApiClient(httpSession);
        ILogStreamClient logStream = new LogStreamClient(httpSession);
        var dashboardVm = new DashboardViewModel(apiClient);
        var containersVm = new ContainersViewModel(apiClient);
        var logsVm = new LogsViewModel(apiClient, logStream);
        var composeVm = new ComposeViewModel(apiClient);
        var imagesVm = new ImagesViewModel(apiClient);
        var cleanupVm = new CleanupViewModel(apiClient);
        var settingsVm = new SettingsViewModel(store, httpSession, apiClient, AppContext.BaseDirectory);
        var appDebugLogVm = new AppDebugLogViewModel();
        var shellVm = new ShellViewModel(dashboardVm, containersVm, logsVm, composeVm, imagesVm, cleanupVm, settingsVm, appDebugLogVm);
        DataContext = shellVm;

        Loaded += async (_, _) =>
        {
            await WslDockerServiceAutoStart.TryEnsureRunningAsync(
                httpSession,
                loaded,
                AppContext.BaseDirectory,
                CancellationToken.None).ConfigureAwait(true);
            await shellVm.Dashboard.RefreshCommand.ExecuteAsync(null);
        };
    }
}
