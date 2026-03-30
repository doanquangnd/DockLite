using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Phản hồi GET /api/compose/projects.
/// </summary>
public sealed class ComposeProjectListApiResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("items")]
    public List<ComposeProjectDto>? Items { get; init; }
}
