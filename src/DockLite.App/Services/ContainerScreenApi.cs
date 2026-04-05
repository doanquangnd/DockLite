using DockLite.Contracts.Api;
using DockLite.Core.Services;

namespace DockLite.App.Services;

/// <summary>
/// Triển khai <see cref="IContainerScreenApi"/> bằng cách ủy quyền tới <see cref="IDockLiteApiClient"/>.
/// </summary>
public sealed class ContainerScreenApi : IContainerScreenApi
{
    private const int TopLimitMin = 1;
    private const int TopLimitMax = 64;

    private readonly IDockLiteApiClient _client;

    public ContainerScreenApi(IDockLiteApiClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public Task<ApiResult<ContainerListData>> GetContainersAsync(CancellationToken cancellationToken = default) =>
        _client.GetContainersAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ContainerTopMemoryData>> GetContainersTopByMemoryAsync(int limit = 5, CancellationToken cancellationToken = default)
    {
        int clamped = Math.Clamp(limit, TopLimitMin, TopLimitMax);
        return _client.GetContainersTopByMemoryAsync(clamped, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ContainerTopMemoryData>> GetContainersTopByCpuAsync(int limit = 5, CancellationToken cancellationToken = default)
    {
        int clamped = Math.Clamp(limit, TopLimitMin, TopLimitMax);
        return _client.GetContainersTopByCpuAsync(clamped, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<EmptyApiPayload>> StartContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ContainerIdError(containerId);
        return err is not null
            ? Task.FromResult(ApiResult<EmptyApiPayload>.Fail(err))
            : _client.StartContainerAsync(containerId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<EmptyApiPayload>> StopContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ContainerIdError(containerId);
        return err is not null
            ? Task.FromResult(ApiResult<EmptyApiPayload>.Fail(err))
            : _client.StopContainerAsync(containerId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<EmptyApiPayload>> RestartContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ContainerIdError(containerId);
        return err is not null
            ? Task.FromResult(ApiResult<EmptyApiPayload>.Fail(err))
            : _client.RestartContainerAsync(containerId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<EmptyApiPayload>> RemoveContainerAsync(string containerId, bool force, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ContainerIdError(containerId);
        return err is not null
            ? Task.FromResult(ApiResult<EmptyApiPayload>.Fail(err))
            : _client.RemoveContainerAsync(containerId, force, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ContainerInspectData>> GetContainerInspectAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ContainerIdError(containerId);
        return err is not null
            ? Task.FromResult(ApiResult<ContainerInspectData>.Fail(err))
            : _client.GetContainerInspectAsync(containerId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ContainerStatsSnapshotData>> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ContainerIdError(containerId);
        return err is not null
            ? Task.FromResult(ApiResult<ContainerStatsSnapshotData>.Fail(err))
            : _client.GetContainerStatsAsync(containerId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ContainerStatsBatchData>> GetContainerStatsBatchAsync(
        IReadOnlyList<string> containerIds,
        CancellationToken cancellationToken = default)
    {
        if (containerIds is null || containerIds.Count == 0)
        {
            return Task.FromResult(
                ApiResult<ContainerStatsBatchData>.Ok(new ContainerStatsBatchData { Items = new List<ContainerStatsBatchItemData>() }));
        }

        return _client.GetContainerStatsBatchAsync(containerIds, cancellationToken);
    }
}
