using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// POST /api/images/pull.
/// </summary>
public sealed class ImagePullRequest
{
    [JsonPropertyName("reference")]
    public string Reference { get; init; } = string.Empty;
}
