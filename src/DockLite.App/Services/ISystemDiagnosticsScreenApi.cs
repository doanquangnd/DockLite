using DockLite.Contracts.Api;

namespace DockLite.App.Services;

/// <summary>
/// Lớp ứng dụng bọc <see cref="IDockLiteApiClient"/> cho trạng thái kết nối service và Docker (health, info) — dùng Dashboard, Cài đặt, Shell.
/// </summary>
public interface ISystemDiagnosticsScreenApi
{
    Task<HealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<DockerInfoData>> GetDockerInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/wsl/host-resources — tài nguyên máy phía distro chạy service.
    /// </summary>
    Task<ApiResult<WslHostResourcesData>> GetWslHostResourcesAsync(CancellationToken cancellationToken = default);
}
