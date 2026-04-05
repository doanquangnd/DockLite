using System.Threading;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Services;
using DockLite.Contracts.Api;

namespace DockLite.App.ViewModels;

/// <summary>
/// Trang tổng quan: trạng thái service WSL và Docker Engine; làm mới định kỳ khi tab đang mở (nhanh khi lỗi, chậm khi ổn).
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private static readonly TimeSpan AutoRefreshIntervalWhenOk = TimeSpan.FromSeconds(55);
    private static readonly TimeSpan AutoRefreshIntervalWhenError = TimeSpan.FromSeconds(14);

    private readonly ISystemDiagnosticsScreenApi _systemDiagnosticsApi;
    private readonly INotificationService _notificationService;
    private readonly AppShellActivityState _shellActivity;
    private readonly IAppShutdownToken _shutdownToken;
    private readonly WslServiceHealthCache _healthCache;

    /// <summary>
    /// Trạng thái kết nối lần trước: null = chưa có lần làm mới thành công.
    /// </summary>
    private bool? _previousConnectivityOk;

    /// <summary>
    /// Lần làm mới gần nhất có health + Docker ok hay không (điều chỉnh chu kỳ tự làm mới).
    /// </summary>
    private bool _lastRefreshOk = true;

    private DispatcherTimer? _autoRefreshTimer;
    private readonly SemaphoreSlim _dashboardRefreshGate = new(1, 1);

    public DashboardViewModel(
        ISystemDiagnosticsScreenApi systemDiagnosticsApi,
        INotificationService notificationService,
        AppShellActivityState shellActivity,
        IAppShutdownToken shutdownToken,
        WslServiceHealthCache healthCache)
    {
        _systemDiagnosticsApi = systemDiagnosticsApi;
        _notificationService = notificationService;
        _shellActivity = shellActivity;
        _shutdownToken = shutdownToken;
        _healthCache = healthCache;
        _shellActivity.Changed += OnShellActivityChanged;
        RestartAutoRefreshTimer();
    }

    private void OnShellActivityChanged(object? sender, EventArgs e)
    {
        RestartAutoRefreshTimer();
    }

    private void StopAutoRefreshTimer()
    {
        if (_autoRefreshTimer is null)
        {
            return;
        }

        _autoRefreshTimer.Tick -= AutoRefreshTimerOnTick;
        _autoRefreshTimer.Stop();
        _autoRefreshTimer = null;
    }

    private void RestartAutoRefreshTimer()
    {
        StopAutoRefreshTimer();
        if (!_shellActivity.ShouldAutoRefreshDashboard)
        {
            return;
        }

        _autoRefreshTimer = new DispatcherTimer
        {
            Interval = _lastRefreshOk ? AutoRefreshIntervalWhenOk : AutoRefreshIntervalWhenError,
        };
        _autoRefreshTimer.Tick += AutoRefreshTimerOnTick;
        _autoRefreshTimer.Start();
    }

    private async void AutoRefreshTimerOnTick(object? sender, EventArgs e)
    {
        if (!_shellActivity.ShouldAutoRefreshDashboard || IsBusy || _shutdownToken.Token.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Đóng app.
        }
    }

    [ObservableProperty]
    private string _serviceHealthText = UiLanguageManager.TryLocalizeCurrent(
        "Ui_Dashboard_ServiceHealth_NotChecked",
        "Chưa kiểm tra");

    [ObservableProperty]
    private string _dockerInfoText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Làm mới health và thông tin Docker (song song).
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!await _dashboardRefreshGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            IsBusy = true;
            DockerInfoText = string.Empty;
            bool refreshOk = false;
            try
            {
                Task<HealthResponse?> healthTask = _systemDiagnosticsApi.GetHealthAsync();
                Task<ApiResult<DockerInfoData>> dockerTask = _systemDiagnosticsApi.GetDockerInfoAsync();
                await Task.WhenAll(healthTask, dockerTask).ConfigureAwait(true);

                HealthResponse? health = await healthTask.ConfigureAwait(true);
                ApiResult<DockerInfoData> docker = await dockerTask.ConfigureAwait(true);

                if (health is null)
                {
                    ServiceHealthText = UiLanguageManager.TryLocalizeCurrent(
                        "Ui_Dashboard_ServiceHealth_NoData",
                        "Không có dữ liệu service.");
                }
                else
                {
                    ServiceHealthText = UiLanguageManager.TryLocalizeFormatCurrent(
                        "Ui_Dashboard_ServiceHealth_LineFormat",
                        "{0} — {1} (phiên bản service: {2})",
                        health.Service,
                        health.Status,
                        health.Version);
                }

                DockerInfoText = FormatDockerInfo(docker);
                bool ok = IsConnectivityOk(health, docker);
                refreshOk = ok;
                await NotifyConnectivityChangeAsync(ok, health, docker).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                string msg = NetworkErrorMessageMapper.FormatForUser(ex);
                ServiceHealthText = UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Dashboard_Status_LoadFailed",
                    "Không tải được trạng thái.");
                DockerInfoText = msg;
                var fail = ApiResult<DockerInfoData>.Fail(new ApiErrorBody { Message = msg });
                await NotifyConnectivityChangeAsync(false, null, fail).ConfigureAwait(true);
                await ApiErrorUiFeedback.ShowNetworkExceptionToastAsync(_notificationService, ex).ConfigureAwait(true);
            }
            finally
            {
                IsBusy = false;
                _lastRefreshOk = refreshOk;
                RestartAutoRefreshTimer();
            }
        }
        finally
        {
            _dashboardRefreshGate.Release();
        }
    }

    private static bool IsConnectivityOk(HealthResponse? health, ApiResult<DockerInfoData> docker) =>
        health is not null && docker.Success && docker.Data is not null;

    private async Task NotifyConnectivityChangeAsync(
        bool currentOk,
        HealthResponse? health,
        ApiResult<DockerInfoData> docker)
    {
        if (currentOk)
        {
            if (health is not null)
            {
                _healthCache.SetFromHealthResponse(health);
            }
        }
        else
        {
            _healthCache.SetFromHealthResponse(null);
        }

        bool first = _previousConnectivityOk is null;
        bool wasOk = _previousConnectivityOk == true;

        if (currentOk)
        {
            if (!first && _previousConnectivityOk == false)
            {
                await _notificationService
                    .ShowAsync(
                        UiLanguageManager.TryLocalizeCurrent("Ui_MainWindow_Title", "DockLite"),
                        UiLanguageManager.TryLocalizeCurrent(
                            "Ui_Dashboard_Notify_Reconnected",
                            "Đã kết nối lại Docker."),
                        NotificationDisplayKind.Success,
                        CancellationToken.None)
                    .ConfigureAwait(true);
            }

            _previousConnectivityOk = true;
            return;
        }

        if (first || wasOk)
        {
            await _notificationService
                .ShowAsync(
                    UiLanguageManager.TryLocalizeCurrent(
                        "Ui_Dashboard_Notify_DisconnectedTitle",
                        "DockLite — mất kết nối"),
                    BuildConnectivityFailureMessage(health, docker),
                    NotificationDisplayKind.Warning,
                    CancellationToken.None)
                .ConfigureAwait(true);
        }

        _previousConnectivityOk = false;
    }

    private static string BuildConnectivityFailureMessage(HealthResponse? health, ApiResult<DockerInfoData> docker)
    {
        if (health is null)
        {
            return UiLanguageManager.TryLocalizeCurrent(
                "Ui_Dashboard_Failure_ServiceNoHttp",
                "Service WSL không phản hồi (HTTP). Kiểm tra dịch vụ đã chạy và địa chỉ/cổng trong cài đặt.");
        }

        if (!docker.Success)
        {
            string m = string.IsNullOrWhiteSpace(docker.Error?.Message)
                ? UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Dashboard_Error_DockerUnavailable",
                    "Docker không khả dụng.")
                : docker.Error!.Message;
            return UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Dashboard_Failure_DockerEngineLine",
                "Docker Engine: {0}",
                m);
        }

        if (docker.Data is null)
        {
            return UiLanguageManager.TryLocalizeCurrent(
                "Ui_Dashboard_Failure_NoDockerData",
                "Không có dữ liệu Docker từ service.");
        }

        return UiLanguageManager.TryLocalizeCurrent(
            "Ui_Dashboard_Failure_Unstable",
            "Kết nối không ổn định.");
    }

    private static string FormatDockerInfo(ApiResult<DockerInfoData> docker)
    {
        if (!docker.Success)
        {
            return string.IsNullOrWhiteSpace(docker.Error?.Message)
                ? UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Dashboard_Docker_NotReady",
                    "Docker không sẵn sàng.")
                : docker.Error!.Message;
        }

        DockerInfoData? d = docker.Data;
        if (d is null)
        {
            return UiLanguageManager.TryLocalizeCurrent(
                "Ui_Dashboard_Docker_NoResponse",
                "Không có phản hồi Docker.");
        }

        return UiLanguageManager.TryLocalizeFormatCurrent(
            "Ui_Dashboard_Docker_InfoFormat",
            "Engine: {0}\nOS: {1} ({2})\nKernel: {3}\nContainer: {4} đang chạy / {5} tổng\nImage: {6}",
            d.ServerVersion ?? string.Empty,
            d.OperatingSystem ?? string.Empty,
            d.OsType ?? string.Empty,
            d.KernelVersion ?? string.Empty,
            d.ContainersRunning,
            d.Containers,
            d.Images);
    }
}
