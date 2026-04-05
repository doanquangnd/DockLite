using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// POST /api/volumes/remove — tên volume Docker (ví dụ từ cột Tên).
/// </summary>
public sealed class VolumeRemoveRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
