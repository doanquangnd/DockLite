using DockLite.Contracts.Api;

namespace DockLite.App.Services;

/// <summary>
/// Bộ nhớ đệm «kết nối đầy đủ» (GET /api/health + GET /api/docker/info thành công), đồng bộ với header và Tổng quan.
/// </summary>
public sealed class WslServiceHealthCache
{
    private bool? _lastHealthy;

    /// <summary>
    /// null: chưa có lần kiểm tra; true: GET /api/health và Docker (envelope) ổn; false: không kết nối hoặc thiếu một trong hai.
    /// </summary>
    public bool? LastHealthy => _lastHealthy;

    public event EventHandler? Changed;

    /// <summary>
    /// Cập nhật cache từ phản hồi health (hoặc null khi lỗi).
    /// </summary>
    /// <param name="forceNotify">
    /// Khi true: luôn gọi <see cref="Changed"/> sau khi cập nhật (ví dụ sau «Kiểm tra kết nối» khi trạng thái healthy không đổi
    /// nhưng cần làm mới dòng header từ GET /api/health).
    /// </param>
    public void SetFromHealthResponse(HealthResponse? health, bool forceNotify = false)
    {
        bool newValue = health is not null;
        bool unchanged = _lastHealthy.HasValue && _lastHealthy.Value == newValue;
        if (unchanged && !forceNotify)
        {
            return;
        }

        _lastHealthy = newValue;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gọi GET /api/health và GET /api/docker/info (song song), cập nhật cache chỉ khi cả hai ổn — cùng tiêu chí với <c>ShellViewModel.RefreshServiceHeaderFromApiAsync</c>.
    /// </summary>
    /// <param name="forceNotify">True sau thao tác thủ công (start/stop WSL) để sidebar/banner đồng bộ kể khi trạng thái không đổi.</param>
    public async Task RefreshAsync(ISystemDiagnosticsScreenApi api, CancellationToken cancellationToken = default, bool forceNotify = false)
    {
        try
        {
            Task<HealthResponse?> healthTask = api.GetHealthAsync(cancellationToken);
            Task<ApiResult<DockerInfoData>> dockerTask = api.GetDockerInfoAsync(cancellationToken);
            await Task.WhenAll(healthTask, dockerTask).ConfigureAwait(false);
            HealthResponse? health = await healthTask.ConfigureAwait(false);
            ApiResult<DockerInfoData> docker = await dockerTask.ConfigureAwait(false);
            bool connectivityOk = health is not null && docker.Success && docker.Data is not null;
            if (connectivityOk)
            {
                SetFromHealthResponse(health, forceNotify);
            }
            else
            {
                SetFromHealthResponse(null, forceNotify);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            SetFromHealthResponse(null, forceNotify);
        }
    }
}
