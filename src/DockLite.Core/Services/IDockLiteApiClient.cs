using DockLite.Contracts.Api;

namespace DockLite.Core.Services;

/// <summary>
/// Client gọi API service DockLite trong WSL (REST, envelope JSON thống nhất trừ /api/health).
/// </summary>
public interface IDockLiteApiClient
{
    /// <summary>
    /// Gọi GET /api/health để kiểm tra service và phiên bản (không dùng envelope).
    /// </summary>
    Task<HealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gọi GET /api/docker/info để kiểm tra Docker Engine trong WSL.
    /// </summary>
    Task<ApiResult<DockerInfoData>> GetDockerInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Danh sách container (tất cả trạng thái).
    /// </summary>
    Task<ApiResult<ContainerListData>> GetContainersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/containers/{id}/start
    /// </summary>
    Task<ApiResult<EmptyApiPayload>> StartContainerAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/containers/{id}/stop
    /// </summary>
    Task<ApiResult<EmptyApiPayload>> StopContainerAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/containers/{id}/restart
    /// </summary>
    Task<ApiResult<EmptyApiPayload>> RestartContainerAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// DELETE /api/containers/{id}
    /// </summary>
    Task<ApiResult<EmptyApiPayload>> RemoveContainerAsync(string containerId, bool force, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/containers/{id}/logs?tail=
    /// </summary>
    Task<ApiResult<ContainerLogsData>> GetContainerLogsAsync(string containerId, int tail, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/containers/{id}/inspect — JSON inspect từ Docker Engine.
    /// </summary>
    Task<ApiResult<ContainerInspectData>> GetContainerInspectAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/containers/{id}/stats — một snapshot CPU/RAM/mạng.
    /// </summary>
    Task<ApiResult<ContainerStatsSnapshotData>> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeProjectListData>> GetComposeProjectsAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeProjectAddData>> AddComposeProjectAsync(ComposeProjectAddRequest request, CancellationToken cancellationToken = default);

    Task<ApiResult<EmptyApiPayload>> RemoveComposeProjectAsync(string projectId, CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeCommandData>> ComposeUpAsync(string projectId, CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeCommandData>> ComposeDownAsync(string projectId, CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeCommandData>> ComposePsAsync(string projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/compose/config/services — tên service (docker compose config --services).
    /// </summary>
    Task<ApiResult<ComposeServiceListData>> ListComposeServicesAsync(string projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/compose/service/start
    /// </summary>
    Task<ApiResult<ComposeCommandData>> ComposeServiceStartAsync(ComposeServiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/compose/service/stop
    /// </summary>
    Task<ApiResult<ComposeCommandData>> ComposeServiceStopAsync(ComposeServiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/compose/service/logs
    /// </summary>
    Task<ApiResult<ComposeCommandData>> ComposeServiceLogsAsync(ComposeServiceLogsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/compose/service/exec — <c>docker compose exec -T</c> (lệnh không TTY).
    /// </summary>
    Task<ApiResult<ComposeCommandData>> ComposeServiceExecAsync(ComposeServiceExecRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/containers/top-by-memory?limit=5 — container đang chạy, sắp theo RAM.
    /// </summary>
    Task<ApiResult<ContainerTopMemoryData>> GetContainersTopByMemoryAsync(int limit = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/containers/top-by-cpu?limit=5 — container đang chạy, sắp theo CPU % (snapshot).
    /// </summary>
    Task<ApiResult<ContainerTopMemoryData>> GetContainersTopByCpuAsync(int limit = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/images
    /// </summary>
    Task<ApiResult<ImageListData>> GetImagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/images/remove
    /// </summary>
    Task<ApiResult<EmptyApiPayload>> RemoveImageAsync(ImageRemoveRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/images/prune
    /// </summary>
    Task<ApiResult<ComposeCommandData>> PruneImagesAsync(ImagePruneRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/system/prune
    /// </summary>
    Task<ApiResult<ComposeCommandData>> SystemPruneAsync(SystemPruneRequest request, CancellationToken cancellationToken = default);
}
