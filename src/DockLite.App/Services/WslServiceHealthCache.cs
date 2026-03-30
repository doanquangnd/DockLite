using DockLite.Contracts.Api;
using DockLite.Core.Services;

namespace DockLite.App.Services;

/// <summary>
/// Trạng thái phản hồi /api/health gần nhất (dùng bật/tắt nút Start/Stop/Restart).
/// </summary>
public sealed class WslServiceHealthCache
{
    private bool? _lastHealthy;

    /// <summary>
    /// null: chưa có lần kiểm tra; true: có phản hồi health; false: không kết nối được hoặc health null.
    /// </summary>
    public bool? LastHealthy => _lastHealthy;

    public event EventHandler? Changed;

    public void SetFromHealthResponse(HealthResponse? health)
    {
        bool newValue = health is not null;
        if (_lastHealthy.HasValue && _lastHealthy.Value == newValue)
        {
            return;
        }

        _lastHealthy = newValue;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task RefreshAsync(IDockLiteApiClient apiClient, CancellationToken cancellationToken = default)
    {
        try
        {
            HealthResponse? health = await apiClient.GetHealthAsync(cancellationToken).ConfigureAwait(false);
            SetFromHealthResponse(health);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            SetFromHealthResponse(null);
        }
    }
}
