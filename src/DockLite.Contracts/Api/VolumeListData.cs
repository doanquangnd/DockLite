using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// GET /api/volumes.
/// </summary>
public sealed class VolumeListData
{
    [JsonPropertyName("items")]
    public List<VolumeSummaryDto> Items { get; init; } = new();
}
