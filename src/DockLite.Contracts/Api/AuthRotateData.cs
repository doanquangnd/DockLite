using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Dữ liệu trả về khi xoay token thành công.
/// </summary>
public sealed class AuthRotateData
{
    [JsonPropertyName("new_token")]
    public string NewToken { get; set; } = string.Empty;
}
