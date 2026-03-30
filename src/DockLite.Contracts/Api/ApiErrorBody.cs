using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Phần error trong envelope khi success = false.
/// </summary>
public sealed class ApiErrorBody
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Tuỳ chọn: stdout/stderr dài (compose, prune) khi lỗi.
    /// </summary>
    [JsonPropertyName("details")]
    public string? Details { get; init; }
}
