using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Nội dung data của GET /api/containers khi thành công.
/// </summary>
public sealed class ContainerListData
{
    [JsonPropertyName("items")]
    public List<ContainerSummaryDto> Items { get; init; } = new();
}
