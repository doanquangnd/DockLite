using DockLite.Contracts.Api;

namespace DockLite.App.Services;

/// <summary>
/// Lớp ứng dụng bọc <see cref="IDockLiteApiClient"/> cho màn Cleanup — tách gọi API khỏi ViewModel.
/// </summary>
public interface ICleanupScreenApi
{
    Task<ApiResult<ComposeCommandData>> SystemPruneAsync(SystemPruneRequest request, CancellationToken cancellationToken = default);
}
