namespace DockLite.Infrastructure.Wsl;

/// <summary>
/// Giai đoạn khởi động tự động (tự khởi động service trong WSL).
/// </summary>
public enum WslStartupPhase
{
    /// <summary>Kiểm tra GET /api/health trước khi spawn WSL.</summary>
    CheckingInitialHealth,

    /// <summary>Đã gọi wsl.exe chạy run-server.sh.</summary>
    LaunchingWslScript,

    /// <summary>Lặp chờ health sau khi spawn (tối đa theo cài đặt WslAutoStartHealthWaitSeconds).</summary>
    WaitingHealthAfterWsl,
}

/// <param name="SecondsRemaining">
/// Số giây còn lại (làm tròn lên); chỉ có nghĩa khi <paramref name="Phase"/> là
/// <see cref="WslStartupPhase.WaitingHealthAfterWsl"/>.
/// </param>
public readonly record struct WslStartupProgress(WslStartupPhase Phase, int? SecondsRemaining);
