using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Một network từ GET /api/networks.
/// </summary>
public sealed class NetworkSummaryDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("driver")]
    public string Driver { get; init; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; init; }
}
