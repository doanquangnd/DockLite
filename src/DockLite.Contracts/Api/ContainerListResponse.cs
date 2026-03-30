using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Phản hồi GET /api/containers.
/// </summary>
public sealed class ContainerListResponse
{
    [JsonPropertyName("items")]
    public List<ContainerSummaryDto> Items { get; init; } = new();

    /// <summary>
    /// Có khi lệnh docker ps thất bại (client vẫn nhận HTTP 200).
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
