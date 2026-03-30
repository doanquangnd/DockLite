using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Một lần chụp thống kê từ GET /api/containers/{id}/stats.
/// </summary>
public sealed class ContainerStatsSnapshotData
{
    [JsonPropertyName("readAt")]
    public string? ReadAt { get; init; }

    [JsonPropertyName("cpuUsagePercent")]
    public double CpuUsagePercent { get; init; }

    [JsonPropertyName("memoryUsageBytes")]
    public ulong MemoryUsageBytes { get; init; }

    [JsonPropertyName("memoryLimitBytes")]
    public ulong MemoryLimitBytes { get; init; }

    [JsonPropertyName("networkRxBytes")]
    public ulong NetworkRxBytes { get; init; }

    [JsonPropertyName("networkTxBytes")]
    public ulong NetworkTxBytes { get; init; }

    /// <summary>
    /// Tổng byte đọc block (blkio), nếu Engine cung cấp.
    /// </summary>
    [JsonPropertyName("blockReadBytes")]
    public ulong BlockReadBytes { get; init; }

    /// <summary>
    /// Tổng byte ghi block (blkio), nếu Engine cung cấp.
    /// </summary>
    [JsonPropertyName("blockWriteBytes")]
    public ulong BlockWriteBytes { get; init; }
}
