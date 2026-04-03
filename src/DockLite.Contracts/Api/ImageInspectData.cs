using System.Text.Json;
using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Dữ liệu GET /api/images/{id}/inspect (trường inspect là JSON thô từ Engine).
/// </summary>
public sealed class ImageInspectData
{
    [JsonPropertyName("inspect")]
    public JsonElement Inspect { get; init; }
}
