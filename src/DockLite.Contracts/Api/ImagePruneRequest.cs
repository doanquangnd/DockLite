using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// POST /api/images/prune (allUnused = true tương đương docker image prune -a -f).
/// </summary>
public sealed class ImagePruneRequest
{
    [JsonPropertyName("allUnused")]
    public bool AllUnused { get; init; }
}
