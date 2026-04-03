namespace DockLite.App.Models;

/// <summary>
/// Một dòng lịch sử image để hiển thị trong lưới (đã định dạng).
/// </summary>
public sealed class ImageHistoryDisplayRow
{
    public string Id { get; init; } = string.Empty;

    public string CreatedText { get; init; } = string.Empty;

    public string CreatedBy { get; init; } = string.Empty;

    public string SizeText { get; init; } = string.Empty;

    public string Comment { get; init; } = string.Empty;
}
