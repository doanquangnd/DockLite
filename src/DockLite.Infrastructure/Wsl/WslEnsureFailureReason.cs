namespace DockLite.Infrastructure.Wsl;

/// <summary>
/// Lý do <see cref="WslDockerServiceAutoStart.TryEnsureRunningAsync"/> không đạt health cuối cùng.
/// </summary>
public enum WslEnsureFailureReason
{
    /// <summary>Service phản hồi health (hoặc không cần chờ).</summary>
    None,

    /// <summary>Tắt tự khởi động WSL và /api/health không thành công.</summary>
    HealthUnavailableWhenAutoStartOff,

    /// <summary>Không xác định được thư mục wsl-docker-service.</summary>
    MissingServiceRoot,

    /// <summary>Thiếu scripts/run-server.sh (khởi động thủ công từ Cài đặt).</summary>
    MissingRunScript,

    /// <summary>Thiếu scripts/restart-server.sh (tự khởi động khi mở app).</summary>
    MissingRestartScript,

    /// <summary>Không chuyển được đường dẫn sang Unix (wslpath).</summary>
    WslPathConversionFailed,

    /// <summary>Đã gọi WSL nhưng không nhận /api/health trong thời gian chờ.</summary>
    HealthTimeoutAfterWslStart,
}
