using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// POST /api/system/prune — kind: containers | images | volumes | networks | system.
/// </summary>
public sealed class SystemPruneRequest
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Chỉ áp dụng khi kind = system (docker system prune --volumes).
    /// </summary>
    [JsonPropertyName("withVolumes")]
    public bool WithVolumes { get; init; }
}
