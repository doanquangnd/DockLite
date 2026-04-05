using DockLite.Contracts.Api;
using DockLite.Core.Services;

namespace DockLite.App.Services;

/// <summary>
/// Triển khai <see cref="INetworkVolumeScreenApi"/> bằng cách ủy quyền tới <see cref="IDockLiteApiClient"/>.
/// </summary>
public sealed class NetworkVolumeScreenApi : INetworkVolumeScreenApi
{
    private readonly IDockLiteApiClient _client;

    public NetworkVolumeScreenApi(IDockLiteApiClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public Task<ApiResult<NetworkListData>> GetNetworksAsync(CancellationToken cancellationToken = default) =>
        _client.GetNetworksAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<VolumeListData>> GetVolumesAsync(CancellationToken cancellationToken = default) =>
        _client.GetVolumesAsync(cancellationToken);

    public Task<ApiResult<EmptyApiPayload>> RemoveVolumeAsync(VolumeRemoveRequest request, CancellationToken cancellationToken = default) =>
        _client.RemoveVolumeAsync(request, cancellationToken);
}
