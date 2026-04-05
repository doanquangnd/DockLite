using DockLite.Contracts.Api;
using DockLite.Core.Services;

namespace DockLite.App.Services;

/// <summary>
/// Triển khai <see cref="ISystemDiagnosticsScreenApi"/> bằng cách ủy quyền tới <see cref="IDockLiteApiClient"/>.
/// </summary>
public sealed class SystemDiagnosticsScreenApi : ISystemDiagnosticsScreenApi
{
    private readonly IDockLiteApiClient _client;

    public SystemDiagnosticsScreenApi(IDockLiteApiClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public Task<HealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default) =>
        _client.GetHealthAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<DockerInfoData>> GetDockerInfoAsync(CancellationToken cancellationToken = default) =>
        _client.GetDockerInfoAsync(cancellationToken);
}
