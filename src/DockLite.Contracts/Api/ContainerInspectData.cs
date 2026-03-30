using System.Text.Json;
using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Nội dung data của GET /api/containers/{id}/inspect khi thành công (trường inspect là JSON thô từ Engine).
/// </summary>
public sealed class ContainerInspectData
{
    [JsonPropertyName("inspect")]
    public JsonElement Inspect { get; init; }
}
