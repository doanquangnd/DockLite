using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Body logs theo service (compose logs --tail).
/// </summary>
public sealed class ComposeServiceLogsRequest
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("service")]
    public string Service { get; init; } = string.Empty;

    [JsonPropertyName("tail")]
    public int Tail { get; init; } = 200;
}
