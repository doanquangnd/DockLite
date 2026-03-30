using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Services;
using DockLite.Contracts.Api;
using DockLite.Core;
using DockLite.Core.Services;

namespace DockLite.App.ViewModels;

/// <summary>
/// Trang tổng quan: trạng thái service WSL và Docker Engine.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IDockLiteApiClient _apiClient;
    private readonly INotificationService _notificationService;

    /// <summary>
    /// Trạng thái kết nối lần trước: null = chưa có lần làm mới thành công.
    /// </summary>
    private bool? _previousConnectivityOk;

    public DashboardViewModel(IDockLiteApiClient apiClient, INotificationService notificationService)
    {
        _apiClient = apiClient;
        _notificationService = notificationService;
    }

    [ObservableProperty]
    private string _serviceHealthText = "Chưa kiểm tra";

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
        IsBusy = true;
        DockerInfoText = string.Empty;
        try
        {
            Task<HealthResponse?> healthTask = _apiClient.GetHealthAsync();
            Task<ApiResult<DockerInfoData>> dockerTask = _apiClient.GetDockerInfoAsync();
            await Task.WhenAll(healthTask, dockerTask).ConfigureAwait(true);

            HealthResponse? health = await healthTask.ConfigureAwait(true);
            ApiResult<DockerInfoData> docker = await dockerTask.ConfigureAwait(true);

            if (health is null)
            {
                ServiceHealthText = "Không có dữ liệu service.";
            }
            else
            {
                ServiceHealthText = $"{health.Service} — {health.Status} (phiên bản service: {health.Version})";
            }

            DockerInfoText = FormatDockerInfo(docker);
            bool ok = IsConnectivityOk(health, docker);
            await NotifyConnectivityChangeAsync(ok, health, docker).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            string msg = ExceptionMessages.FormatForUser(ex);
            ServiceHealthText = "Không tải được trạng thái.";
            DockerInfoText = msg;
            var fail = ApiResult<DockerInfoData>.Fail(new ApiErrorBody { Message = msg });
            await NotifyConnectivityChangeAsync(false, null, fail).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool IsConnectivityOk(HealthResponse? health, ApiResult<DockerInfoData> docker) =>
        health is not null && docker.Success && docker.Data is not null;

    private async Task NotifyConnectivityChangeAsync(
        bool currentOk,
        HealthResponse? health,
        ApiResult<DockerInfoData> docker)
    {
        bool first = _previousConnectivityOk is null;
        bool wasOk = _previousConnectivityOk == true;

        if (currentOk)
        {
            if (!first && _previousConnectivityOk == false)
            {
                await _notificationService
                    .ShowAsync(
                        "DockLite",
                        "Đã kết nối lại Docker.",
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
                    "DockLite — mất kết nối",
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
            return "Service WSL không phản hồi (HTTP). Kiểm tra dịch vụ đã chạy và địa chỉ/cổng trong cài đặt.";
        }

        if (!docker.Success)
        {
            string m = string.IsNullOrWhiteSpace(docker.Error?.Message) ? "Docker không khả dụng." : docker.Error!.Message;
            return "Docker Engine: " + m;
        }

        if (docker.Data is null)
        {
            return "Không có dữ liệu Docker từ service.";
        }

        return "Kết nối không ổn định.";
    }

    private static string FormatDockerInfo(ApiResult<DockerInfoData> docker)
    {
        if (!docker.Success)
        {
            return string.IsNullOrWhiteSpace(docker.Error?.Message) ? "Docker không sẵn sàng." : docker.Error!.Message;
        }

        DockerInfoData? d = docker.Data;
        if (d is null)
        {
            return "Không có phản hồi Docker.";
        }

        return
            $"Engine: {d.ServerVersion}\n" +
            $"OS: {d.OperatingSystem} ({d.OsType})\n" +
            $"Kernel: {d.KernelVersion}\n" +
            $"Container: {d.ContainersRunning} đang chạy / {d.Containers} tổng\n" +
            $"Image: {d.Images}";
    }
}
