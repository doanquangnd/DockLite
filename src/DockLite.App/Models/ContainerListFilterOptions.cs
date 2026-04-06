namespace DockLite.App.Models;

/// <summary>
/// Lọc danh sách container theo trạng thái chạy/dừng (không phụ thuộc chuỗi hiển thị đã dịch).
/// </summary>
public enum ContainerListFilterKind
{
    All,
    Running,
    Stopped,
}

/// <summary>
/// Giới hạn trường tìm kiếm (ô tìm).
/// </summary>
public enum ContainerSearchScope
{
    /// <summary>Tên, image, ID, trạng thái, cổng, lệnh, nhãn.</summary>
    All,

    /// <summary>Tên và ID (đầy đủ / rút gọn).</summary>
    Name,

    /// <summary>Image.</summary>
    Image,

    /// <summary>Chuỗi trạng thái (ví dụ Up, Exited).</summary>
    Status,
}

/// <summary>
/// Một mục ComboBox lọc trạng thái.
/// </summary>
public sealed class FilterKindOption
{
    public required ContainerListFilterKind Kind { get; init; }

    public required string Label { get; init; }
}

/// <summary>
/// Một mục ComboBox phạm vi tìm.
/// </summary>
public sealed class SearchScopeOption
{
    public required ContainerSearchScope Scope { get; init; }

    public required string Label { get; init; }
}
