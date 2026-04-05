using DockLite.Contracts.Api;

namespace DockLite.App.Services;

/// <summary>
/// Trạng thái phản hồi /api/health gần nhất (dùng bật/tắt nút Start/Stop/Restart).
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
    /// Gọi GET /api/health và cập nhật cache.
    /// </summary>
    /// <param name="forceNotify">True sau thao tác thủ công (start/stop WSL) để header đồng bộ kể khi healthy không đổi.</param>
    public async Task RefreshAsync(ISystemDiagnosticsScreenApi api, CancellationToken cancellationToken = default, bool forceNotify = false)
    {
        try
        {
            HealthResponse? health = await api.GetHealthAsync(cancellationToken).ConfigureAwait(false);
            SetFromHealthResponse(health, forceNotify);
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
