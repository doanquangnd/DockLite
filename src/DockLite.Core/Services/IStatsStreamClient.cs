using DockLite.Contracts.Api;

namespace DockLite.Core.Services;

/// <summary>
/// WebSocket tới /ws/containers/{id}/stats — mỗi tin nhắn một JSON <see cref="ContainerStatsSnapshotData"/>.
/// </summary>
public interface IStatsStreamClient
{
    /// <summary>
    /// Mở WebSocket, gọi <paramref name="onSample"/> cho mỗi mẫu stats (thread pool; UI cần marshal).
    /// </summary>
    Task StreamStatsAsync(
        string containerId,
        int intervalMs,
        Action<ContainerStatsSnapshotData> onSample,
        CancellationToken cancellationToken);
}
