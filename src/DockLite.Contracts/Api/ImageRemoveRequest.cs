using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// POST /api/images/remove.
/// </summary>
public sealed class ImageRemoveRequest
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}
