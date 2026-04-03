using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// GET /api/networks.
/// </summary>
public sealed class NetworkListData
{
    [JsonPropertyName("items")]
    public List<NetworkSummaryDto> Items { get; init; } = new();
}
