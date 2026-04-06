using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Kết quả văn bản từ <c>trivy image</c>.
/// </summary>
public sealed class ImageTrivyScanResultData
{
    [JsonPropertyName("output")]
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Định dạng đã áp dụng sau chuẩn hóa (table hoặc json).
    /// </summary>
    [JsonPropertyName("format")]
    public string? Format { get; init; }
}
