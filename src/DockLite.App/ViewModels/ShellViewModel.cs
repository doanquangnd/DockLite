using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DockLite.App.ViewModels;

/// <summary>
/// ViewModel vỏ ứng dụng: sidebar và trang hiện tại.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    public ShellViewModel(
        DashboardViewModel dashboard,
        ContainersViewModel containers,
        LogsViewModel logs,
        ComposeViewModel compose,
        ImagesViewModel images,
        CleanupViewModel cleanup,
        SettingsViewModel settings,
        AppDebugLogViewModel appDebugLog)
    {
        Dashboard = dashboard;
        Containers = containers;
        Logs = logs;
        Compose = compose;
        Images = images;
        Cleanup = cleanup;
        Settings = settings;
        AppDebugLog = appDebugLog;
        CurrentPage = dashboard;
    }

    public DashboardViewModel Dashboard { get; }

    public ContainersViewModel Containers { get; }

    public LogsViewModel Logs { get; }

    public ComposeViewModel Compose { get; }

    public ImagesViewModel Images { get; }

    public CleanupViewModel Cleanup { get; }

    public SettingsViewModel Settings { get; }

    public AppDebugLogViewModel AppDebugLog { get; }

    [ObservableProperty]
    private object? _currentPage;

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
        await Logs.LoadContainersCommand.ExecuteAsync(null);
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
}
