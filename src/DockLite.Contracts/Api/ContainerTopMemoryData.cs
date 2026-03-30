using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Phản hồi GET /api/containers/top-by-memory — top container đang chạy theo RAM (snapshot).
/// </summary>
public sealed class ContainerTopMemoryData
{
    [JsonPropertyName("items")]
    public List<ContainerTopMemoryRowDto> Items { get; init; } = new();
}

/// <summary>
/// Một dòng trong bảng top RAM.
/// </summary>
public sealed class ContainerTopMemoryRowDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("shortId")]
    public string ShortId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("image")]
    public string Image { get; init; } = string.Empty;

    [JsonPropertyName("memoryUsageBytes")]
    public ulong MemoryUsageBytes { get; init; }

    [JsonPropertyName("cpuUsagePercent")]
    public double CpuUsagePercent { get; init; }
}
