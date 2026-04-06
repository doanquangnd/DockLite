using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Body các lệnh compose theo id project đã lưu.
/// </summary>
public sealed class ComposeIdRequest
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Tùy chọn: <c>docker compose --profile</c> (lặp nhiều lần).
    /// </summary>
    [JsonPropertyName("profiles")]
    public IReadOnlyList<string>? Profiles { get; init; }
}
