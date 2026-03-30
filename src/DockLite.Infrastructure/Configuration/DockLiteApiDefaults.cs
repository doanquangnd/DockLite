using DockLite.Core.Configuration;

namespace DockLite.Infrastructure.Configuration;

/// <summary>
/// Giá trị mặc định kết nối tới service Go trong WSL (localhost forward).
/// </summary>
public static class DockLiteApiDefaults
{
    /// <summary>
    /// Cổng mặc định; có thể đổi sau qua cấu hình ứng dụng.
    /// </summary>
    public const int DefaultPort = 17890;

    /// <summary>
    /// Base URL kết thúc bằng / để ghép đường dẫn tương đối ổn định.
    /// </summary>
    public static string DefaultBaseUrl => DockLiteDefaults.ServiceBaseUrl;
}
