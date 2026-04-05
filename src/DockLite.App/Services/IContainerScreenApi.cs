using DockLite.Contracts.Api;

namespace DockLite.App.Services;

/// <summary>
/// Lớp ứng dụng bọc <see cref="IDockLiteApiClient"/> cho màn Container — tách gọi API khỏi ViewModel, dễ kiểm thử/mở rộng.
/// </summary>
public interface IContainerScreenApi
{
    Task<ApiResult<ContainerListData>> GetContainersAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<ContainerTopMemoryData>> GetContainersTopByMemoryAsync(int limit = 5, CancellationToken cancellationToken = default);

    Task<ApiResult<ContainerTopMemoryData>> GetContainersTopByCpuAsync(int limit = 5, CancellationToken cancellationToken = default);

    Task<ApiResult<EmptyApiPayload>> StartContainerAsync(string containerId, CancellationToken cancellationToken = default);

    Task<ApiResult<EmptyApiPayload>> StopContainerAsync(string containerId, CancellationToken cancellationToken = default);

    Task<ApiResult<EmptyApiPayload>> RestartContainerAsync(string containerId, CancellationToken cancellationToken = default);

    Task<ApiResult<EmptyApiPayload>> RemoveContainerAsync(string containerId, bool force, CancellationToken cancellationToken = default);

    Task<ApiResult<ContainerInspectData>> GetContainerInspectAsync(string containerId, CancellationToken cancellationToken = default);

    Task<ApiResult<ContainerStatsSnapshotData>> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default);

    Task<ApiResult<ContainerStatsBatchData>> GetContainerStatsBatchAsync(
        IReadOnlyList<string> containerIds,
        CancellationToken cancellationToken = default);
}
