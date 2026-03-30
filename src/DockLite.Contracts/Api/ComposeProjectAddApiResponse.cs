using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Phản hồi POST /api/compose/projects.
/// </summary>
public sealed class ComposeProjectAddApiResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("project")]
    public ComposeProjectDto? Project { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
