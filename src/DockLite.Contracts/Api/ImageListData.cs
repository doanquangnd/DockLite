using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Nội dung data của GET /api/images khi thành công.
/// </summary>
public sealed class ImageListData
{
    [JsonPropertyName("items")]
    public List<ImageSummaryDto> Items { get; init; } = new();
}
