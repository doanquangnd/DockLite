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
    private readonly DockLiteHttpSession _httpSession;
    private readonly WslServiceHealthCache _healthCache;
    private readonly string _appBaseDirectory;
    private readonly IAppShutdownToken _shutdownToken;

    /// <summary>
    /// Tránh vòng lặp: RefreshServiceHeaderFromApiAsync gọi SetFromHealthResponse trong khi handler Changed cũng gọi Refresh.
    /// </summary>
    private bool _suppressHealthCacheHeaderSync;

    public ShellViewModel(
        DashboardViewModel dashboard,
        ContainersViewModel containers,
        LogsViewModel logs,
        ComposeViewModel compose,
        ImagesViewModel images,
        CleanupViewModel cleanup,
        SettingsViewModel settings,
        AppDebugLogViewModel appDebugLog,
        IDockLiteApiClient apiClient,
        IDialogService dialogService,
        DockLiteHttpSession httpSession,
        WslServiceHealthCache healthCache,
        string appBaseDirectory,
        IAppShutdownToken shutdownToken)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;
        _httpSession = httpSession;
        _healthCache = healthCache;
        _appBaseDirectory = appBaseDirectory;
        _shutdownToken = shutdownToken;
        _healthCache.Changed += OnHealthCacheChanged;
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
    private string _serviceHeaderPrimaryText = "Đang khởi động…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasServiceHeaderSecondary))]
    private string _serviceHeaderSecondaryText = "";

    /// <summary>Dòng phụ (timeout) có hiển thị hay không.</summary>
    public bool HasServiceHeaderSecondary => !string.IsNullOrEmpty(ServiceHeaderSecondaryText);

    /// <summary>Tiến trình Start/Stop/Restart từ header (tách với IsBusy của từng trang).</summary>
    [ObservableProperty]
    private bool _isWslServiceCommandBusy;

    public bool CanHeaderStartWslService => !IsWslServiceCommandBusy && _healthCache.LastHealthy != true;

    public bool CanHeaderStopWslService => !IsWslServiceCommandBusy && _healthCache.LastHealthy == true;

    public bool CanHeaderRestartWslService => !IsWslServiceCommandBusy && _healthCache.LastHealthy == true;

    [ObservableProperty]
    private object? _currentPage;

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
                ServiceHeaderPrimaryText = "Đang kiểm tra kết nối tới service WSL…";
                ServiceHeaderSecondaryText = "";
                break;
            case WslStartupPhase.LaunchingWslScript:
                ServiceHeaderPrimaryText = "Đang gửi lệnh tới WSL (run-server.sh)…";
                ServiceHeaderSecondaryText = "";
                break;
            case WslStartupPhase.WaitingHealthAfterWsl:
                ServiceHeaderPrimaryText = "Chờ phản hồi /api/health sau khi khởi động WSL";
                int total = WslDockerServiceAutoStart.GetHealthWaitAfterWslSeconds(Settings.GetSettingsSnapshotForWslCommands());
                int sec = p.SecondsRemaining ?? 0;
                ServiceHeaderSecondaryText = $"Còn ~{sec}s / {total}s (timeout)";
                break;
        }
    }

    /// <summary>
    /// Đồng bộ dòng trạng thái với GET /api/health (sau khởi động hoặc khi cần làm mới).
    /// </summary>
    public async Task RefreshServiceHeaderFromApiAsync(CancellationToken cancellationToken = default)
    {
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

    /// <summary>
    /// Hiển thị hộp thoại trợ giúp theo màn hình đang mở (CurrentPage).
    /// </summary>
    [RelayCommand]
    private async Task ShowHelpAsync()
    {
        (string shortTitle, string body) = PageHelpTexts.GetForCurrentPage(CurrentPage);
        await _dialogService.ShowInfoAsync(body, "Trợ giúp — " + shortTitle).ConfigureAwait(true);
    }
}
