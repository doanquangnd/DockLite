using DockLite.Contracts.Api;

namespace DockLite.App.Services;

/// <summary>
/// Lớp ứng dụng bọc <see cref="IDockLiteApiClient"/> cho màn Logs — tách gọi API khỏi ViewModel.
/// </summary>
public interface ILogsScreenApi
{
    Task<ApiResult<ContainerListData>> GetContainersAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<ContainerLogsData>> GetContainerLogsAsync(string containerId, int tail, CancellationToken cancellationToken = default);
}
