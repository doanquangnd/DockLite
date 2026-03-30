using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Body POST /api/compose/projects (ưu tiên windowsPath; có thể bổ sung wslPath sau).
/// </summary>
public sealed class ComposeProjectAddRequest
{
    [JsonPropertyName("windowsPath")]
    public string WindowsPath { get; init; } = string.Empty;

    [JsonPropertyName("wslPath")]
    public string? WslPath { get; init; }
}
