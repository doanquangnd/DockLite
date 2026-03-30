using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Body start/stop một service trong project.
/// </summary>
public sealed class ComposeServiceRequest
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("service")]
    public string Service { get; init; } = string.Empty;
}
