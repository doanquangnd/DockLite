using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// GET /api/images/{id}/history.
/// </summary>
public sealed class ImageHistoryData
{
    [JsonPropertyName("items")]
    public List<ImageHistoryLayerDto> Items { get; init; } = new();
}
