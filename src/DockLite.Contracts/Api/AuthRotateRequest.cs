using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Thân POST /api/auth/rotate
/// </summary>
public sealed class AuthRotateRequest
{
    [JsonPropertyName("current_token")]
    public string? CurrentToken { get; set; }
}
