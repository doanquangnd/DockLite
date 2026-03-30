using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public DashboardViewModel(IDockLiteApiClient apiClient)
    {
        _apiClient = apiClient;
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
            Task<DockerInfoResponse?> dockerTask = _apiClient.GetDockerInfoAsync();
            await Task.WhenAll(healthTask, dockerTask).ConfigureAwait(true);

            HealthResponse? health = await healthTask.ConfigureAwait(true);
            DockerInfoResponse? docker = await dockerTask.ConfigureAwait(true);

            if (health is null)
            {
                ServiceHealthText = "Không có dữ liệu service.";
            }
            else
            {
                ServiceHealthText = $"{health.Service} — {health.Status} (phiên bản service: {health.Version})";
            }

            DockerInfoText = FormatDockerInfo(docker);
        }
        catch (Exception ex)
        {
            string msg = ExceptionMessages.FormatForUser(ex);
            ServiceHealthText = "Không tải được trạng thái.";
            DockerInfoText = msg;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatDockerInfo(DockerInfoResponse? docker)
    {
        if (docker is null)
        {
            return "Không có phản hồi Docker.";
        }

        if (!docker.Ok)
        {
            return string.IsNullOrWhiteSpace(docker.Error) ? "Docker không sẵn sàng." : docker.Error;
        }

        return
            $"Engine: {docker.ServerVersion}\n" +
            $"OS: {docker.OperatingSystem} ({docker.OsType})\n" +
            $"Kernel: {docker.KernelVersion}\n" +
            $"Container: {docker.ContainersRunning} đang chạy / {docker.Containers} tổng\n" +
            $"Image: {docker.Images}";
    }
}
