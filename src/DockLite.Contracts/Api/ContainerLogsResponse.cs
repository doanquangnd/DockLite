using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Phản hồi GET /api/containers/{id}/logs?tail=
/// </summary>
public sealed class ContainerLogsResponse
{
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
