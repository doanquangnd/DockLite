using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Một volume từ GET /api/volumes.
/// </summary>
public sealed class VolumeSummaryDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("driver")]
    public string Driver { get; init; } = string.Empty;

    [JsonPropertyName("mountpoint")]
    public string Mountpoint { get; init; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; init; }
}
