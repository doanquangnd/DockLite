namespace DockLite.App;

/// <summary>
/// Ngữ cảnh host ứng dụng (đường dẫn gốc để spawn WSL, đọc log, v.v.).
/// </summary>
public sealed record AppHostContext(string BaseDirectory);
