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
    /// Token tùy chọn gửi kèm <c>Authorization: Bearer</c> tới service Go khi biến môi trường <c>DOCKLITE_API_TOKEN</c> được đặt phía WSL. Để trống = không gửi (tương thích cấu hình cũ).
    /// </summary>
    public string? ServiceApiToken { get; set; }

    /// <summary>
    /// Thời gian chờ HTTP (giây), khoảng 30–600. Mặc định 120.
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Khi mở app, nếu service chưa phản hồi thì gọi wsl chạy wsl-docker-service (mặc định bật).
    /// </summary>
    public bool AutoStartWslService { get; set; } = true;

    /// <summary>
    /// Đường dẫn Windows tới thư mục wsl-docker-service dùng cho script (run/build/stop). Để trống để tự tìm từ thư mục exe.
    /// </summary>
    public string? WslDockerServiceWindowsPath { get; set; }

    /// <summary>
    /// Đường dẫn Windows tới thư mục nguồn khi đồng bộ (thường là clone trên ổ C:). Để trống thì đồng bộ dùng cùng đường dẫn với <see cref="WslDockerServiceWindowsPath"/> (hoặc tự tìm).
    /// </summary>
    public string? WslDockerServiceSyncSourceWindowsPath { get; set; }

    /// <summary>
    /// Tên distro WSL (ví dụ Ubuntu). Để trống dùng mặc định của wsl.exe.
    /// </summary>
    public string? WslDistribution { get; set; }

    /// <summary>
    /// Đường dẫn Unix trong WSL làm đích khi đồng bộ (ví dụ /home/user/wsl-docker-service). Để trống nếu không dùng đồng bộ từ GUI.
    /// </summary>
    public string? WslDockerServiceLinuxSyncPath { get; set; }

    /// <summary>
    /// Khi đồng bộ: xóa file ở đích không còn ở nguồn (chỉ khi có rsync trong WSL).
    /// </summary>
    public bool WslDockerServiceSyncDeleteExtra { get; set; }

    /// <summary>
    /// Chỉ cho phép đồng bộ khi version trong file VERSION ở nguồn (Windows) >= version ở đích (WSL); cần file VERSION ở cả hai phía khi bật.
    /// </summary>
    public bool WslDockerServiceSyncEnforceVersionGe { get; set; }

    /// <summary>
    /// Múi giờ hiển thị (Windows <see cref="TimeZoneInfo.Id"/>). Để trống hoặc null = <see cref="TimeZoneInfo.Local"/>.
    /// </summary>
    public string? UiTimeZoneId { get; set; }

    /// <summary>
    /// Chuỗi định dạng ngày giờ cho UI (hợp lệ với <see cref="DateTime.ToString(string, IFormatProvider)"/>).
    /// </summary>
    public string UiDateTimeFormat { get; set; } = "yyyy/MM/dd HH:mm:ss";

    /// <summary>
    /// Chủ đề giao diện: Light hoặc Dark (áp dụng sau khi khởi động lại ứng dụng).
    /// </summary>
    public string UiTheme { get; set; } = "Light";

    /// <summary>
    /// Ngôn ngữ chuỗi giao diện: vi hoặc en (áp dụng sau Lưu hoặc khi load).
    /// </summary>
    public string UiLanguage { get; set; } = "vi";

    /// <summary>
    /// Sau khi tự spawn WSL khi mở app, chờ /api/health tối đa bao nhiêu giây (10–600, mặc định 30).
    /// </summary>
    public int WslAutoStartHealthWaitSeconds { get; set; } = 30;

    /// <summary>
    /// Start/Restart thủ công: chờ /api/health tối đa bao nhiêu giây (10–600, mặc định 90).
    /// </summary>
    public int WslManualHealthWaitSeconds { get; set; } = 90;

    /// <summary>
    /// Timeout một lần gọi GET /api/health khi poll (1–60 giây, mặc định 3).
    /// </summary>
    public int HealthProbeSingleRequestSeconds { get; set; } = 3;

    /// <summary>
    /// Khoảng cách giữa các lần poll health khi chờ WSL (100–5000 ms, mặc định 500).
    /// </summary>
    public int WslHealthPollIntervalMilliseconds { get; set; } = 500;

    /// <summary>
    /// Khi bật: ghi file <c>docklite-diagnostic-*.log</c> cùng thư mục log ứng dụng — sự kiện kết nối/health tối giản, không gửi mạng, không chứa mật khẩu hay thân API.
    /// </summary>
    public bool DiagnosticLocalTelemetryEnabled { get; set; }
}
