using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Phản hồi POST /api/images/load (thông điệp từ Docker sau khi load).
/// </summary>
public sealed class ImageLoadResultData
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}
