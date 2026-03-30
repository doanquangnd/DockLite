using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Body gọi <c>docker compose exec -T</c> (không TTY); lệnh tách bằng khoảng trắng.
/// </summary>
public sealed class ComposeServiceExecRequest
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("service")]
    public string Service { get; init; } = string.Empty;

    /// <summary>
    /// Ví dụ: <c>uname -a</c> (các từ tách bằng khoảng trắng).
    /// </summary>
    [JsonPropertyName("command")]
    public string Command { get; init; } = string.Empty;
}
