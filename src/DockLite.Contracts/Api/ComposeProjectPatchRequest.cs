using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Body PATCH /api/compose/projects/{id} — cập nhật chuỗi file <c>-f</c>.
/// </summary>
public sealed class ComposeProjectPatchRequest
{
    [JsonPropertyName("composeFiles")]
    public List<string> ComposeFiles { get; init; } = new();
}
