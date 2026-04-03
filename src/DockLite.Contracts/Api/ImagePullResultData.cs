using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Phản hồi pull image (log stream rút gọn từ daemon).
/// </summary>
public sealed class ImagePullResultData
{
    [JsonPropertyName("log")]
    public string Log { get; init; } = string.Empty;
}
