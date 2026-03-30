using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Một dòng container từ GET /api/containers.
/// </summary>
public sealed class ContainerSummaryDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("shortId")]
    public string ShortId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("image")]
    public string Image { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("ports")]
    public string? Ports { get; init; }

    [JsonPropertyName("command")]
    public string? Command { get; init; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; init; }
}
