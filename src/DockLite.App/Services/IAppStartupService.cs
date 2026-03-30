namespace DockLite.App.Services;

/// <summary>
/// Luồng khởi động sau khi cửa sổ chính load (WSL auto-start, làm mới dashboard).
/// </summary>
public interface IAppStartupService
{
    /// <summary>
    /// Đảm bảo service (nếu bật tự khởi động) rồi làm mới bảng điều khiển.
    /// </summary>
    Task RunInitialLoadAsync(CancellationToken cancellationToken = default);
}
