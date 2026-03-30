using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Phản hồi GET /api/images.
/// </summary>
public sealed class ImageListResponse
{
    [JsonPropertyName("items")]
    public List<ImageSummaryDto> Items { get; init; } = new();

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
