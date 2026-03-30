namespace DockLite.Core.Services;

/// <summary>
/// Kết nối WebSocket tới /ws/containers/{id}/logs và nhận chunk văn bản.
/// </summary>
public interface ILogStreamClient
{
    /// <summary>
    /// Mở WebSocket, gọi <paramref name="onChunk"/> cho mỗi tin nhắn văn bản hoàn chỉnh.
    /// </summary>
    Task StreamLogsAsync(string containerId, Action<string> onChunk, CancellationToken cancellationToken);
}
