using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Nội dung data của GET /api/compose/projects khi thành công.
/// </summary>
public sealed class ComposeProjectListData
{
    [JsonPropertyName("items")]
    public List<ComposeProjectDto>? Items { get; init; }
}
