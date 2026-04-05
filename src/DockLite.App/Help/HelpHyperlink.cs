namespace DockLite.App.Help;

/// <summary>
/// Một liên kết hiển thị trong hộp thoại trợ giúp (mở bằng trình duyệt mặc định).
/// </summary>
public readonly record struct HelpHyperlink(string DisplayText, Uri Target);
