using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Một layer trong lịch sử image (docker history).
/// </summary>
public sealed class ImageHistoryLayerDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("created")]
    public long Created { get; init; }

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }

    [JsonPropertyName("comment")]
    public string Comment { get; init; } = string.Empty;
}
