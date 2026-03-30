using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Nội dung data của compose up/down/ps, image prune, system prune khi thành công.
/// </summary>
public sealed class ComposeCommandData
{
    [JsonPropertyName("output")]
    public string? Output { get; init; }
}
