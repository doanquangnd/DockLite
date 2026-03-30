using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Nội dung data của GET /api/containers/.../logs khi thành công.
/// </summary>
public sealed class ContainerLogsData
{
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}
