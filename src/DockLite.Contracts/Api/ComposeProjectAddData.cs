using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Nội dung data của POST /api/compose/projects khi thành công.
/// </summary>
public sealed class ComposeProjectAddData
{
    [JsonPropertyName("project")]
    public ComposeProjectDto? Project { get; init; }
}
