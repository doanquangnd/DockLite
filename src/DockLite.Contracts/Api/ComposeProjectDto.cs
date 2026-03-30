using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Một project Compose đã lưu trên service WSL.
/// </summary>
public sealed class ComposeProjectDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("wslPath")]
    public string WslPath { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
