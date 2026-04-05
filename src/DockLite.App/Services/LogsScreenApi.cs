using DockLite.Contracts.Api;
using DockLite.Core.Services;

namespace DockLite.App.Services;

/// <summary>
/// Triển khai <see cref="ILogsScreenApi"/> bằng cách ủy quyền tới <see cref="IDockLiteApiClient"/>.
/// </summary>
public sealed class LogsScreenApi : ILogsScreenApi
{
    private readonly IDockLiteApiClient _client;

    public LogsScreenApi(IDockLiteApiClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public Task<ApiResult<ContainerListData>> GetContainersAsync(CancellationToken cancellationToken = default) =>
        _client.GetContainersAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ContainerLogsData>> GetContainerLogsAsync(string containerId, int tail, CancellationToken cancellationToken = default) =>
        _client.GetContainerLogsAsync(containerId, tail, cancellationToken);
}
