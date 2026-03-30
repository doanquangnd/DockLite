namespace DockLite.Core.Configuration;

/// <summary>
/// Cấu hình đọc/ghi từ file local (địa chỉ service WSL).
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Base URL kết thúc bằng / khuyến nghị.
    /// </summary>
    public string ServiceBaseUrl { get; set; } = DockLiteDefaults.ServiceBaseUrl;

    /// <summary>
    /// Thời gian chờ HTTP (giây), khoảng 30–600. Mặc định 120.
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Khi mở app, nếu service chưa phản hồi thì gọi wsl chạy wsl-docker-service (mặc định bật).
    /// </summary>
    public bool AutoStartWslService { get; set; } = true;

    /// <summary>
    /// Đường dẫn Windows tới thư mục wsl-docker-service. Để trống để tự tìm từ thư mục exe.
    /// </summary>
    public string? WslDockerServiceWindowsPath { get; set; }

    /// <summary>
    /// Tên distro WSL (ví dụ Ubuntu). Để trống dùng mặc định của wsl.exe.
    /// </summary>
    public string? WslDistribution { get; set; }
}
