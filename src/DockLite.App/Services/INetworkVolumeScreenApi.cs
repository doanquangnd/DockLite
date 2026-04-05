using DockLite.Contracts.Api;

namespace DockLite.App.Services;

/// <summary>
/// Lớp ứng dụng bọc <see cref="IDockLiteApiClient"/> cho màn Network/Volume — tách gọi API khỏi ViewModel.
/// </summary>
public interface INetworkVolumeScreenApi
{
    Task<ApiResult<NetworkListData>> GetNetworksAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<VolumeListData>> GetVolumesAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<EmptyApiPayload>> RemoveVolumeAsync(VolumeRemoveRequest request, CancellationToken cancellationToken = default);
}
