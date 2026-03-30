using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Phản hồi chung cho thao tác POST/DELETE (start/stop/restart/rm).
/// </summary>
public sealed class ApiActionResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
