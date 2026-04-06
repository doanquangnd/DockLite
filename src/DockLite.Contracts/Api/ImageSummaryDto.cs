using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Một dòng image từ GET /api/images.
/// </summary>
public sealed class ImageSummaryDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("repository")]
    public string Repository { get; init; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public string Size { get; init; } = string.Empty;

    /// <summary>
    /// Kích thước image (byte) từ Docker; dùng gộp tổng theo ID không trùng tag.
    /// </summary>
    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; init; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; init; }
}
