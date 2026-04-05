using DockLite.Contracts.Api;
using DockLite.Core.Services;

namespace DockLite.App.Services;

/// <summary>
/// Triển khai <see cref="ICleanupScreenApi"/> bằng cách ủy quyền tới <see cref="IDockLiteApiClient"/>.
/// </summary>
public sealed class CleanupScreenApi : ICleanupScreenApi
{
    private readonly IDockLiteApiClient _client;

    public CleanupScreenApi(IDockLiteApiClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public Task<ApiResult<ComposeCommandData>> SystemPruneAsync(SystemPruneRequest request, CancellationToken cancellationToken = default) =>
        _client.SystemPruneAsync(request, cancellationToken);
}
