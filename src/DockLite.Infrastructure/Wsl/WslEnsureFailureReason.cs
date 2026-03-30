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

    /// <summary>Thiếu scripts/run-server.sh.</summary>
    MissingRunScript,

    /// <summary>Không chuyển được đường dẫn sang Unix (wslpath).</summary>
    WslPathConversionFailed,

    /// <summary>Đã gọi WSL nhưng không nhận /api/health trong thời gian chờ.</summary>
    HealthTimeoutAfterWslStart,
}
