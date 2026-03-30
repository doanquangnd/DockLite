using DockLite.Contracts.Api;

namespace DockLite.Core.Services;

/// <summary>
/// Client gọi API service DockLite trong WSL (REST).
/// </summary>
public interface IDockLiteApiClient
{
    /// <summary>
    /// Gọi GET /api/health để kiểm tra service và phiên bản.
    /// </summary>
    Task<HealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gọi GET /api/docker/info để kiểm tra Docker Engine trong WSL.
    /// </summary>
    Task<DockerInfoResponse?> GetDockerInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Danh sách container (tất cả trạng thái).
    /// </summary>
    Task<ContainerListResponse?> GetContainersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/containers/{id}/start
    /// </summary>
    Task<ApiActionResponse?> StartContainerAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/containers/{id}/stop
    /// </summary>
    Task<ApiActionResponse?> StopContainerAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/containers/{id}/restart
    /// </summary>
    Task<ApiActionResponse?> RestartContainerAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// DELETE /api/containers/{id}
    /// </summary>
    Task<ApiActionResponse?> RemoveContainerAsync(string containerId, bool force, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/containers/{id}/logs?tail=
    /// </summary>
    Task<ContainerLogsResponse?> GetContainerLogsAsync(string containerId, int tail, CancellationToken cancellationToken = default);

    Task<ComposeProjectListApiResponse?> GetComposeProjectsAsync(CancellationToken cancellationToken = default);

    Task<ComposeProjectAddApiResponse?> AddComposeProjectAsync(ComposeProjectAddRequest request, CancellationToken cancellationToken = default);

    Task<ApiActionResponse?> RemoveComposeProjectAsync(string projectId, CancellationToken cancellationToken = default);

    Task<ComposeCommandResponse?> ComposeUpAsync(string projectId, CancellationToken cancellationToken = default);

    Task<ComposeCommandResponse?> ComposeDownAsync(string projectId, CancellationToken cancellationToken = default);

    Task<ComposeCommandResponse?> ComposePsAsync(string projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/images
    /// </summary>
    Task<ImageListResponse?> GetImagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/images/remove
    /// </summary>
    Task<ApiActionResponse?> RemoveImageAsync(ImageRemoveRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/images/prune
    /// </summary>
    Task<ComposeCommandResponse?> PruneImagesAsync(ImagePruneRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/system/prune
    /// </summary>
    Task<ComposeCommandResponse?> SystemPruneAsync(SystemPruneRequest request, CancellationToken cancellationToken = default);
}
