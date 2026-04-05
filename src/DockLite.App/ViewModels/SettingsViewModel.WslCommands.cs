using CommunityToolkit.Mvvm.Input;
using DockLite.App.Services;
using DockLite.Contracts.Api;
using DockLite.Core.Configuration;
using DockLite.Core.Diagnostics;
using DockLite.Infrastructure.Wsl;

namespace DockLite.App.ViewModels;

/// <summary>
/// Lệnh WSL thủ công, đồng bộ mã và kiểm tra kết nối (tách khỏi file ViewModel chính).
/// </summary>
public partial class SettingsViewModel
{
    [RelayCommand]
    private async Task StartWslServiceManualAsync()
    {
        IsBusy = true;
        StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_StartServiceProgress", "Đang áp dụng địa chỉ trong ô và khởi động service...");
        try
        {
            AppSettings snapshot = CreateSettingsSnapshotForWsl();
            // HttpClient phải trùng URL trong ô khi chờ health (không bắt buộc đã Lưu ra file).
            _httpSession.Reconfigure(snapshot);
            (bool sent, bool healthOk, string msg) = await WslDockerServiceAutoStart
                .TryStartServiceManuallyAndWaitForHealthAsync(_httpSession, snapshot, _appBaseDirectory, _shutdownToken.Token)
                .ConfigureAwait(true);
            DiagnosticTelemetry.WriteManualWslLifecycle(snapshot, "settings", "start", sent, healthOk);
            StatusMessage = msg;
            AppFileLog.Write("WSL thủ công", msg + (sent && healthOk ? " [health OK]" : ""));
            await _healthCache.RefreshAsync(_systemDiagnosticsApi, _shutdownToken.Token, forceNotify: true).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_CancelledAppExit", "Đã hủy (đóng ứng dụng).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StopWslServiceManualAsync()
    {
        IsBusy = true;
        StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_StopServiceProgress", "Đang gửi lệnh dừng service trong WSL...");
        try
        {
            AppSettings snapshot = CreateSettingsSnapshotForWsl();
            _httpSession.Reconfigure(snapshot);
            if (!WslDockerServiceAutoStart.TryStopServiceManually(snapshot, _appBaseDirectory, out string msg))
            {
                DiagnosticTelemetry.WriteManualWslLifecycle(snapshot, "settings", "stop", false);
                StatusMessage = msg;
                await _healthCache.RefreshAsync(_systemDiagnosticsApi, _shutdownToken.Token, forceNotify: true).ConfigureAwait(true);
                return;
            }

            DiagnosticTelemetry.WriteManualWslLifecycle(snapshot, "settings", "stop", true);
            StatusMessage = msg;
            await Task.Delay(800, _shutdownToken.Token).ConfigureAwait(true);
            await _healthCache.RefreshAsync(_systemDiagnosticsApi, _shutdownToken.Token, forceNotify: true).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_CancelledAppExit", "Đã hủy (đóng ứng dụng).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestartWslServiceManualAsync()
    {
        IsBusy = true;
        StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_RestartServiceProgress", "Đang restart service trong WSL...");
        try
        {
            AppSettings snapshot = CreateSettingsSnapshotForWsl();
            _httpSession.Reconfigure(snapshot);
            (bool sent, bool healthOk, string msg) = await WslDockerServiceAutoStart
                .TryRestartServiceManuallyAndWaitForHealthAsync(_httpSession, snapshot, _appBaseDirectory, _shutdownToken.Token)
                .ConfigureAwait(true);
            DiagnosticTelemetry.WriteManualWslLifecycle(snapshot, "settings", "restart", sent, healthOk);
            StatusMessage = msg;
            AppFileLog.Write("WSL restart thủ công", msg + (sent && healthOk ? " [health OK]" : ""));
            await _healthCache.RefreshAsync(_systemDiagnosticsApi, _shutdownToken.Token, forceNotify: true).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_CancelledAppExit", "Đã hủy (đóng ứng dụng).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BuildWslServiceManualAsync()
    {
        IsBusy = true;
        StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_BuildServiceProgress", "Đang gửi lệnh build (go) trong WSL...");
        try
        {
            AppSettings snapshot = CreateSettingsSnapshotForWsl();
            _httpSession.Reconfigure(snapshot);
            if (!WslDockerServiceAutoStart.TryBuildServiceManually(snapshot, _appBaseDirectory, out string msg))
            {
                DiagnosticTelemetry.WriteManualWslLifecycle(snapshot, "settings", "build", false);
                StatusMessage = msg;
                return;
            }

            DiagnosticTelemetry.WriteManualWslLifecycle(snapshot, "settings", "build", true);
            StatusMessage = msg;
            await Task.Delay(400, _shutdownToken.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_CancelledAppExit", "Đã hủy (đóng ứng dụng).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Sao chép mã từ ô «Nguồn trong Windows» (hoặc cùng thư mục dịch vụ nếu để trống) sang đích Unix.
    /// </summary>
    [RelayCommand]
    private async Task SyncServiceSourceToWslAsync()
    {
        IsBusy = true;
        StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_SyncSourceProgress", "Đang đồng bộ mã nguồn vào WSL...");
        AppSettings snapshot = CreateSettingsSnapshotForWsl();
        try
        {
            (bool ok, string msg) = await WslDockerServiceAutoStart
                .TrySyncWindowsSourceToLinuxDestinationAsync(snapshot, _appBaseDirectory, _shutdownToken.Token)
                .ConfigureAwait(true);
            DiagnosticTelemetry.WriteSyncCodeToWsl(snapshot, ok);
            StatusMessage = msg;
            AppFileLog.Write("WSL đồng bộ mã", msg + (ok ? " [OK]" : ""));
            if (!ok)
            {
                await _notificationService
                    .ShowAsync(
                        "DockLite — đồng bộ mã WSL",
                        TruncateForToast(msg, ToastMessageMaxChars),
                        NotificationDisplayKind.Warning,
                        _shutdownToken.Token)
                    .ConfigureAwait(true);
            }
            else
            {
                await _notificationService
                    .ShowAsync(
                        "DockLite — đồng bộ mã WSL",
                        TruncateForToast(msg, ToastMessageMaxChars),
                        NotificationDisplayKind.Success,
                        _shutdownToken.Token)
                    .ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_CancelledAppExit", "Đã hủy (đóng ứng dụng).");
        }
        catch (Exception ex)
        {
            DiagnosticTelemetry.WriteSyncCodeToWsl(snapshot, false);
            StatusMessage = ex.Message;
            AppFileLog.Write("WSL đồng bộ mã", "Lỗi: " + ex);
            await _notificationService
                .ShowAsync(
                    "DockLite — đồng bộ mã WSL",
                    TruncateForToast(ex.Message, ToastMessageMaxChars),
                    NotificationDisplayKind.Warning,
                    _shutdownToken.Token)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string TruncateForToast(string message, int max)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        message = message.Trim();
        return message.Length <= max ? message : message.Substring(0, max) + "…";
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        AppSettings snapshot = CreateSettingsSnapshotForWsl();
        try
        {
            var health = await _systemDiagnosticsApi.GetHealthAsync(_shutdownToken.Token).ConfigureAwait(true);
            _healthCache.SetFromHealthResponse(health, forceNotify: true);
            ApiResult<DockerInfoData> docker = await _systemDiagnosticsApi.GetDockerInfoAsync(_shutdownToken.Token).ConfigureAwait(true);
            string h = health is null ? "—" : $"{health.Service} ({health.Status})";
            string d;
            if (docker.Success && docker.Data is not null)
            {
                string ver = docker.Data.ServerVersion ?? "?";
                string os = docker.Data.OperatingSystem ?? docker.Data.OsType ?? "?";
                d = $"Docker Engine: {ver} ({os})";
            }
            else
            {
                d = docker.Error?.Message ?? UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_DockerErrorGeneric", "Lỗi Docker");
            }

            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent("Ui_Settings_Status_TestConnectionOkFormat", "Service: {0} | {1}", h, d);
            DiagnosticTelemetry.WriteTestConnection(snapshot, true, null);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_CancelledAppExit", "Đã hủy (đóng ứng dụng).");
        }
        catch (Exception ex)
        {
            DiagnosticTelemetry.WriteTestConnection(snapshot, false, ex.GetType().Name);
            _healthCache.SetFromHealthResponse(null, forceNotify: true);
            AppFileLog.WriteException("Kiểm tra kết nối", ex);
            string msg = NetworkErrorMessageMapper.FormatForUser(ex);
            // Gợi ý chạy binary chỉ khi không có mã HTTP (lỗi tầng kết nối), không thêm khi đã có 401/403/404/5xx từ server.
            if (ex is System.Net.Http.HttpRequestException hre && !hre.StatusCode.HasValue)
            {
                msg += UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Settings_Status_TestConnectionHttpHint",
                    " Gợi ý: trong WSL (đúng distro chứa project) chạy ./bin/docklite-wsl; chỉ go build là chưa đủ. Nếu có nhiều distro WSL, điền Ubuntu-22.04 vào Distro WSL rồi Lưu để tự khởi động đúng máy.");
            }

            StatusMessage = msg;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
