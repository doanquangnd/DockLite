namespace DockLite.App.Models;

/// <summary>
/// Lọc danh sách image: có tag hay chỉ dangling (repository «none» theo API).
/// </summary>
public enum ImageListFilterKind
{
    All,
    Tagged,
    Dangling,
}

/// <summary>
/// Giới hạn trường ô tìm trên danh sách image.
/// </summary>
public enum ImageSearchScope
{
    /// <summary>Repository, tag, id, kích thước, thời điểm tạo.</summary>
    All,

    Repository,
    Tag,
    Id,
}

/// <summary>
/// Một mục ComboBox lọc (image).
/// </summary>
public sealed class ImageFilterKindOption
{
    public required ImageListFilterKind Kind { get; init; }

    public required string Label { get; init; }
}

/// <summary>
/// Một mục ComboBox phạm vi tìm (image).
/// </summary>
public sealed class ImageSearchScopeOption
{
    public required ImageSearchScope Scope { get; init; }

    public required string Label { get; init; }
}
