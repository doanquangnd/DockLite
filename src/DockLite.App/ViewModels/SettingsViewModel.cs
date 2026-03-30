using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.Core;
using DockLite.Core.Configuration;
using DockLite.Core.Diagnostics;
using DockLite.Core.Services;
using DockLite.Infrastructure.Api;
using DockLite.Infrastructure.Configuration;
using DockLite.Infrastructure.Wsl;

namespace DockLite.App.ViewModels;

/// <summary>
/// Trang cài đặt: địa chỉ base URL service, timeout HTTP và kiểm tra kết nối.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsStore _store;
    private readonly DockLiteHttpSession _httpSession;
    private readonly IDockLiteApiClient _apiClient;
    private readonly string _appBaseDirectory;

    public SettingsViewModel(
        IAppSettingsStore store,
        DockLiteHttpSession httpSession,
        IDockLiteApiClient apiClient,
        string appBaseDirectory)
    {
        _store = store;
        _httpSession = httpSession;
        _apiClient = apiClient;
        _appBaseDirectory = appBaseDirectory;
        AppSettings loaded = _store.Load();
        ServiceBaseUrl = loaded.ServiceBaseUrl;
        int sec = loaded.HttpTimeoutSeconds >= 30 ? loaded.HttpTimeoutSeconds : 120;
        HttpTimeoutSecondsText = sec.ToString();
        AutoStartWslService = loaded.AutoStartWslService;
        WslDockerServiceWindowsPath = loaded.WslDockerServiceWindowsPath ?? string.Empty;
        WslDistribution = loaded.WslDistribution ?? string.Empty;
    }

    [ObservableProperty]
    private string _serviceBaseUrl = string.Empty;

    [ObservableProperty]
    private bool _autoStartWslService = true;

    [ObservableProperty]
    private string _wslDockerServiceWindowsPath = string.Empty;

    [ObservableProperty]
    private string _wslDistribution = string.Empty;

    /// <summary>
    /// Chuỗi số giây (30–600), parse khi Lưu.
    /// </summary>
    [ObservableProperty]
    private string _httpTimeoutSecondsText = "120";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private void ApplyWslIp()
    {
        if (WslHostAddressResolver.TryGetFirstIpv4(WslDistribution, out string ip))
        {
            ServiceBaseUrl = $"http://{ip}:{DockLiteApiDefaults.DefaultPort}/";
            StatusMessage =
                $"Đã điền địa chỉ theo IP WSL ({ip}). Nhấn Lưu để áp dụng cho toàn bộ ứng dụng.";
            return;
        }

        StatusMessage =
            "Không lấy được IP WSL (wsl hostname -I). Kiểm tra WSL đã bật; nếu có nhiều distro, điền tên Distro WSL rồi thử lại.";
    }

    [RelayCommand]
    private void Save()
    {
        if (!int.TryParse(HttpTimeoutSecondsText.Trim(), out int sec) || sec < 30 || sec > 600)
        {
            StatusMessage = "Timeout phải là số nguyên từ 30 đến 600 giây.";
            return;
        }

        var settings = new AppSettings
        {
            ServiceBaseUrl = ServiceBaseUrl.Trim(),
            HttpTimeoutSeconds = sec,
            AutoStartWslService = AutoStartWslService,
            WslDockerServiceWindowsPath = string.IsNullOrWhiteSpace(WslDockerServiceWindowsPath)
                ? null
                : WslDockerServiceWindowsPath.Trim(),
            WslDistribution = string.IsNullOrWhiteSpace(WslDistribution) ? null : WslDistribution.Trim(),
        };
        _store.Save(settings);
        _httpSession.Reconfigure(settings);
        string hostSummary = settings.ServiceBaseUrl;
        try
        {
            var u = new Uri(settings.ServiceBaseUrl);
            hostSummary = u.Host + ":" + u.Port;
        }
        catch
        {
            // giữ nguyên chuỗi gốc
        }

        AppFileLog.Write(
            "Cài đặt",
            "Đã lưu. Đích: " + hostSummary + ", timeout=" + sec + "s, tự khởi động WSL=" + settings.AutoStartWslService);
        StatusMessage = "Đã lưu. Địa chỉ và timeout đã áp dụng cho các lần gọi tiếp theo.";
    }

    [RelayCommand]
    private async Task StartWslServiceManualAsync()
    {
        IsBusy = true;
        StatusMessage = "Đang áp dụng địa chỉ trong ô và khởi động service...";
        try
        {
            AppSettings snapshot = CreateSettingsSnapshotForWsl();
            // HttpClient phải trùng URL trong ô khi chờ health (không bắt buộc đã Lưu ra file).
            _httpSession.Reconfigure(snapshot);
            (bool sent, bool healthOk, string msg) = await WslDockerServiceAutoStart
                .TryStartServiceManuallyAndWaitForHealthAsync(_httpSession, snapshot, _appBaseDirectory, CancellationToken.None)
                .ConfigureAwait(true);
            StatusMessage = msg;
            AppFileLog.Write("WSL thủ công", msg + (sent && healthOk ? " [health OK]" : ""));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Ảnh chụp cấu hình từ ô hiện tại (dùng cho khởi động WSL, không cần Lưu trước).
    /// </summary>
    private AppSettings CreateSettingsSnapshotForWsl()
    {
        int sec = 120;
        if (int.TryParse(HttpTimeoutSecondsText.Trim(), out int t) && t >= 30 && t <= 600)
        {
            sec = t;
        }

        return new AppSettings
        {
            ServiceBaseUrl = ServiceBaseUrl.Trim(),
            HttpTimeoutSeconds = sec,
            AutoStartWslService = AutoStartWslService,
            WslDockerServiceWindowsPath = string.IsNullOrWhiteSpace(WslDockerServiceWindowsPath)
                ? null
                : WslDockerServiceWindowsPath.Trim(),
            WslDistribution = string.IsNullOrWhiteSpace(WslDistribution) ? null : WslDistribution.Trim(),
        };
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var health = await _apiClient.GetHealthAsync().ConfigureAwait(true);
            var docker = await _apiClient.GetDockerInfoAsync().ConfigureAwait(true);
            string h = health is null ? "—" : $"{health.Service} ({health.Status})";
            string d = docker is null ? "—" : (docker.Ok ? "Docker OK" : docker.Error ?? "Lỗi Docker");
            StatusMessage = $"Service: {h} | {d}";
        }
        catch (Exception ex)
        {
            AppFileLog.WriteException("Kiểm tra kết nối", ex);
            string msg = ExceptionMessages.FormatForUser(ex);
            if (ex is System.Net.Http.HttpRequestException)
            {
                msg += " Gợi ý: trong WSL (đúng distro chứa project) chạy ./bin/docklite-wsl; chỉ go build là chưa đủ. Nếu có nhiều distro WSL, điền Ubuntu-22.04 vào Distro WSL rồi Lưu để tự khởi động đúng máy.";
            }

            StatusMessage = msg;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
