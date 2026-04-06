using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Body quét CVE bằng Trivy (công cụ ngoài trên WSL).
/// </summary>
public sealed class ImageTrivyScanRequest
{
    [JsonPropertyName("imageRef")]
    public string ImageRef { get; init; } = string.Empty;

    /// <summary>
    /// Định dạng đầu ra Trivy: <c>table</c> hoặc <c>json</c> (mặc định phía service: table).
    /// </summary>
    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; init; }

    /// <summary>
    /// Đường dẫn tuyệt đối trong WSL tới thư mục hoặc file policy Rego (tùy chọn, truyền cho <c>--policy</c>).
    /// </summary>
    [JsonPropertyName("policyPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyPath { get; init; }
}
