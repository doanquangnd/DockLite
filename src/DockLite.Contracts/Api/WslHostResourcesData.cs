using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Nội dung data của GET /api/wsl/host-resources (snapshot RAM, load, đĩa gốc trong distro WSL).
/// </summary>
public sealed class WslHostResourcesData
{
    [JsonPropertyName("memoryTotalKb")]
    public long MemoryTotalKb { get; init; }

    [JsonPropertyName("memoryAvailableKb")]
    public long MemoryAvailableKb { get; init; }

    [JsonPropertyName("memoryUsedPercent")]
    public double MemoryUsedPercent { get; init; }

    [JsonPropertyName("loadAvg1")]
    public double LoadAvg1 { get; init; }

    [JsonPropertyName("loadAvg5")]
    public double LoadAvg5 { get; init; }

    [JsonPropertyName("loadAvg15")]
    public double LoadAvg15 { get; init; }

    [JsonPropertyName("rootMountPath")]
    public string RootMountPath { get; init; } = "/";

    [JsonPropertyName("diskRootTotalBytes")]
    public long DiskRootTotalBytes { get; init; }

    [JsonPropertyName("diskRootAvailableBytes")]
    public long DiskRootAvailableBytes { get; init; }

    [JsonPropertyName("diskRootUsedPercent")]
    public double DiskRootUsedPercent { get; init; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; init; }

    [JsonPropertyName("cpuCoresOnline")]
    public int CpuCoresOnline { get; init; }

    [JsonPropertyName("collectedAtUtcIso")]
    public string CollectedAtUtcIso { get; init; } = string.Empty;
}
