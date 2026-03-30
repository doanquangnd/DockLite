using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Services;
using DockLite.Contracts.Api;
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
    private readonly IAppShutdownToken _shutdownToken;
    private readonly WslServiceHealthCache _healthCache;
    private readonly AppUiDisplaySettings _uiDisplay;

    public SettingsViewModel(
        IAppSettingsStore store,
        DockLiteHttpSession httpSession,
        IDockLiteApiClient apiClient,
        string appBaseDirectory,
        AppSettings initialSettings,
        IAppShutdownToken shutdownToken,
        WslServiceHealthCache healthCache,
        AppUiDisplaySettings uiDisplay)
    {
        _store = store;
        _httpSession = httpSession;
        _apiClient = apiClient;
        _appBaseDirectory = appBaseDirectory;
        _shutdownToken = shutdownToken;
        _healthCache = healthCache;
        _uiDisplay = uiDisplay;
        _healthCache.Changed += (_, _) => NotifyWslServiceButtonStates();
        ServiceBaseUrl = initialSettings.ServiceBaseUrl;
        int sec = initialSettings.HttpTimeoutSeconds >= 30 ? initialSettings.HttpTimeoutSeconds : 120;
        HttpTimeoutSecondsText = sec.ToString();
        AutoStartWslService = initialSettings.AutoStartWslService;
        WslDockerServiceWindowsPath = initialSettings.WslDockerServiceWindowsPath ?? string.Empty;
        WslDistribution = initialSettings.WslDistribution ?? string.Empty;
        SelectedUiTimeZoneId = initialSettings.UiTimeZoneId ?? string.Empty;
        UiDateTimeFormat = string.IsNullOrWhiteSpace(initialSettings.UiDateTimeFormat)
            ? "yyyy/MM/dd HH:mm:ss"
            : initialSettings.UiDateTimeFormat;
        WslAutoStartHealthWaitSecondsText = initialSettings.WslAutoStartHealthWaitSeconds.ToString();
        WslManualHealthWaitSecondsText = initialSettings.WslManualHealthWaitSeconds.ToString();
        HealthProbeSingleRequestSecondsText = initialSettings.HealthProbeSingleRequestSeconds.ToString();
        WslHealthPollIntervalMillisecondsText = initialSettings.WslHealthPollIntervalMilliseconds.ToString();
        TimeZoneOptions.Add(new TimeZoneOptionItem { Id = "", DisplayName = "Giờ máy (Windows local)" });
        foreach (TimeZoneInfo z in TimeZoneInfo.GetSystemTimeZones().OrderBy(x => x.DisplayName))
        {
            TimeZoneOptions.Add(new TimeZoneOptionItem { Id = z.Id, DisplayName = z.DisplayName });
        }

        EffectiveWslPathSummary = BuildEffectiveWslPathSummary();
        UpdateDatePreview();
    }

    public bool CanStartWslServiceButton => !IsBusy && _healthCache.LastHealthy != true;

    public bool CanStopWslServiceButton => !IsBusy && _healthCache.LastHealthy == true;

    public bool CanRestartWslServiceButton => !IsBusy && _healthCache.LastHealthy == true;

    /// <summary>
    /// Ảnh chụp cấu hình từ ô hiện tại (Start/Stop/Restart từ header và Cài đặt).
    /// </summary>
    public AppSettings GetSettingsSnapshotForWslCommands() => CreateSettingsSnapshotForWsl();

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

    /// <summary>
    /// Đường dẫn Windows để thử lệnh wslpath (mục 5 kế hoạch).
    /// </summary>
    [ObservableProperty]
    private string _wslpathProbeWindowsPath = string.Empty;

    /// <summary>
    /// Kết quả wslpath hoặc thông báo lỗi.
    /// </summary>
    [ObservableProperty]
    private string _wslpathProbeResult = string.Empty;

    /// <summary>
    /// Kết quả chạy <c>uname -a</c> trong distro (nút Thử distro).
    /// </summary>
    [ObservableProperty]
    private string _distroTestResult = string.Empty;

    /// <summary>
    /// Tóm tắt distro, đường dẫn service Windows→WSL, base URL.
    /// </summary>
    [ObservableProperty]
    private string _effectiveWslPathSummary = string.Empty;

    /// <summary>
    /// Giá trị <see cref="TimeZoneInfo.Id"/>; chuỗi rỗng = giờ máy.
    /// </summary>
    [ObservableProperty]
    private string _selectedUiTimeZoneId = string.Empty;

    [ObservableProperty]
    private string _uiDateTimeFormat = "yyyy/MM/dd HH:mm:ss";

    [ObservableProperty]
    private string _wslAutoStartHealthWaitSecondsText = "30";

    [ObservableProperty]
    private string _wslManualHealthWaitSecondsText = "90";

    [ObservableProperty]
    private string _healthProbeSingleRequestSecondsText = "3";

    [ObservableProperty]
    private string _wslHealthPollIntervalMillisecondsText = "500";

    [ObservableProperty]
    private string _dateFormatPreviewText = string.Empty;

    public ObservableCollection<TimeZoneOptionItem> TimeZoneOptions { get; } = new();

    /// <summary>
    /// Gợi ý nhanh định dạng (nút gán chuỗi vào ô định dạng).
    /// </summary>
    public string[] DateFormatPresets { get; } =
    {
        "yyyy/MM/dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "G",
        "g",
        "dd/MM/yyyy",
        "HH:mm:ss",
    };

    partial void OnUiDateTimeFormatChanged(string value)
    {
        UpdateDatePreview();
    }

    partial void OnSelectedUiTimeZoneIdChanged(string value)
    {
        UpdateDatePreview();
    }

    private void UpdateDatePreview()
    {
        DateFormatPreviewText = "UTC hiện tại → " + _uiDisplay.PreviewFormatUtc(
            DateTime.UtcNow,
            string.IsNullOrWhiteSpace(SelectedUiTimeZoneId) ? null : SelectedUiTimeZoneId,
            UiDateTimeFormat);
    }

    [RelayCommand]
    private void ApplyDateFormatPreset(string? format)
    {
        if (!string.IsNullOrWhiteSpace(format))
        {
            UiDateTimeFormat = format.Trim();
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyWslServiceButtonStates();
    }

    private void NotifyWslServiceButtonStates()
    {
        OnPropertyChanged(nameof(CanStartWslServiceButton));
        OnPropertyChanged(nameof(CanStopWslServiceButton));
        OnPropertyChanged(nameof(CanRestartWslServiceButton));
    }

    [RelayCommand]
    private void ProbeWslpath()
    {
        WslpathProbeResult = string.Empty;
        string? distro = string.IsNullOrWhiteSpace(WslDistribution) ? null : WslDistribution.Trim();
        if (!WslPathProbe.TryWindowsToUnix(WslpathProbeWindowsPath, distro, out string unix, out string? err))
        {
            WslpathProbeResult = err ?? "Lỗi wslpath.";
            EffectiveWslPathSummary = BuildEffectiveWslPathSummary();
            return;
        }

        WslpathProbeResult = unix;
        EffectiveWslPathSummary = BuildEffectiveWslPathSummary();
    }

    /// <summary>
    /// Chạy <c>uname -a</c> trong distro đã nhập (hoặc distro mặc định).
    /// </summary>
    [RelayCommand]
    private async Task TestDistroAsync()
    {
        IsBusy = true;
        DistroTestResult = string.Empty;
        try
        {
            string? distro = string.IsNullOrWhiteSpace(WslDistribution) ? null : WslDistribution.Trim();
            (bool ok, string stdout, string? err) = await Task.Run(() =>
            {
                if (WslDistroProbe.TryRunUname(distro, out string so, out string? e))
                {
                    return (true, so, (string?)null);
                }

                return (false, string.Empty, e);
            }).ConfigureAwait(true);

            DistroTestResult = ok ? stdout : (err ?? "Không chạy được uname trong WSL.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RefreshEffectiveWslPathSummary()
    {
        EffectiveWslPathSummary = BuildEffectiveWslPathSummary();
    }

    private string BuildEffectiveWslPathSummary()
    {
        var sb = new StringBuilder();
        if (string.IsNullOrWhiteSpace(WslDistribution))
        {
            sb.AppendLine("Distro WSL: (để trống — wsl.exe dùng distro mặc định).");
        }
        else
        {
            sb.Append("Distro WSL: ").AppendLine(WslDistribution.Trim());
        }

        string path = WslDockerServiceWindowsPath.Trim();
        if (string.IsNullOrEmpty(path))
        {
            sb.AppendLine("Thư mục wsl-docker-service (Windows): (trống — DockLite tự tìm thư mục wsl-docker-service cạnh file chạy ứng dụng).");
        }
        else
        {
            string? distro = string.IsNullOrWhiteSpace(WslDistribution) ? null : WslDistribution.Trim();
            if (WslPathProbe.TryWindowsToUnix(path, distro, out string unix, out string? err))
            {
                sb.Append("Thư mục service (Windows): ").AppendLine(path);
                sb.Append("→ trong WSL: ").AppendLine(unix);
            }
            else
            {
                sb.Append("Thư mục service (Windows): ").AppendLine(path);
                sb.Append("→ wslpath: ").AppendLine(err ?? "lỗi");
            }
        }

        sb.Append("Base URL (ô phía trên): ").Append(ServiceBaseUrl.Trim());
        return sb.ToString();
    }

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

        if (!int.TryParse(WslAutoStartHealthWaitSecondsText.Trim(), out int autoW) || autoW < 10 || autoW > 600)
        {
            StatusMessage = "Chờ health khi tự khởi động WSL: số nguyên từ 10 đến 600 giây.";
            return;
        }

        if (!int.TryParse(WslManualHealthWaitSecondsText.Trim(), out int manW) || manW < 10 || manW > 600)
        {
            StatusMessage = "Chờ health khi Start/Restart thủ công: số nguyên từ 10 đến 600 giây.";
            return;
        }

        if (!int.TryParse(HealthProbeSingleRequestSecondsText.Trim(), out int probe) || probe < 1 || probe > 60)
        {
            StatusMessage = "Timeout một lần gọi /api/health: từ 1 đến 60 giây.";
            return;
        }

        if (!int.TryParse(WslHealthPollIntervalMillisecondsText.Trim(), out int pollMs) || pollMs < 100 || pollMs > 5000)
        {
            StatusMessage = "Khoảng cách poll health: từ 100 đến 5000 ms.";
            return;
        }

        if (string.IsNullOrWhiteSpace(UiDateTimeFormat))
        {
            StatusMessage = "Định dạng ngày giờ không được để trống.";
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
            UiTimeZoneId = string.IsNullOrWhiteSpace(SelectedUiTimeZoneId) ? null : SelectedUiTimeZoneId.Trim(),
            UiDateTimeFormat = UiDateTimeFormat.Trim(),
            WslAutoStartHealthWaitSeconds = autoW,
            WslManualHealthWaitSeconds = manW,
            HealthProbeSingleRequestSeconds = probe,
            WslHealthPollIntervalMilliseconds = pollMs,
        };
        AppSettingsDefaults.Normalize(settings);
        _store.Save(settings);
        _httpSession.Reconfigure(settings);
        _uiDisplay.Apply(settings);
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
        StatusMessage = "Đã lưu. Địa chỉ, timeout, hiển thị thời gian và chờ health đã áp dụng.";
        EffectiveWslPathSummary = BuildEffectiveWslPathSummary();
        UpdateDatePreview();
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
                .TryStartServiceManuallyAndWaitForHealthAsync(_httpSession, snapshot, _appBaseDirectory, _shutdownToken.Token)
                .ConfigureAwait(true);
            StatusMessage = msg;
            AppFileLog.Write("WSL thủ công", msg + (sent && healthOk ? " [health OK]" : ""));
            await _healthCache.RefreshAsync(_apiClient, _shutdownToken.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy (đóng ứng dụng).";
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
        StatusMessage = "Đang gửi lệnh dừng service trong WSL...";
        try
        {
            AppSettings snapshot = CreateSettingsSnapshotForWsl();
            _httpSession.Reconfigure(snapshot);
            if (!WslDockerServiceAutoStart.TryStopServiceManually(snapshot, _appBaseDirectory, out string msg))
            {
                StatusMessage = msg;
                await _healthCache.RefreshAsync(_apiClient, _shutdownToken.Token).ConfigureAwait(true);
                return;
            }

            StatusMessage = msg;
            await Task.Delay(800, _shutdownToken.Token).ConfigureAwait(true);
            await _healthCache.RefreshAsync(_apiClient, _shutdownToken.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy (đóng ứng dụng).";
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
        StatusMessage = "Đang restart service trong WSL...";
        try
        {
            AppSettings snapshot = CreateSettingsSnapshotForWsl();
            _httpSession.Reconfigure(snapshot);
            (bool sent, bool healthOk, string msg) = await WslDockerServiceAutoStart
                .TryRestartServiceManuallyAndWaitForHealthAsync(_httpSession, snapshot, _appBaseDirectory, _shutdownToken.Token)
                .ConfigureAwait(true);
            StatusMessage = msg;
            AppFileLog.Write("WSL restart thủ công", msg + (sent && healthOk ? " [health OK]" : ""));
            await _healthCache.RefreshAsync(_apiClient, _shutdownToken.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy (đóng ứng dụng).";
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
        StatusMessage = "Đang gửi lệnh build (go) trong WSL...";
        try
        {
            AppSettings snapshot = CreateSettingsSnapshotForWsl();
            _httpSession.Reconfigure(snapshot);
            if (!WslDockerServiceAutoStart.TryBuildServiceManually(snapshot, _appBaseDirectory, out string msg))
            {
                StatusMessage = msg;
                return;
            }

            StatusMessage = msg;
            await Task.Delay(400, _shutdownToken.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy (đóng ứng dụng).";
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

        int autoW = 45;
        if (int.TryParse(WslAutoStartHealthWaitSecondsText.Trim(), out int aw) && aw is >= 10 and <= 600)
        {
            autoW = aw;
        }

        int manW = 90;
        if (int.TryParse(WslManualHealthWaitSecondsText.Trim(), out int mw) && mw is >= 10 and <= 600)
        {
            manW = mw;
        }

        int probe = 3;
        if (int.TryParse(HealthProbeSingleRequestSecondsText.Trim(), out int pr) && pr is >= 1 and <= 60)
        {
            probe = pr;
        }

        int pollMs = 500;
        if (int.TryParse(WslHealthPollIntervalMillisecondsText.Trim(), out int pm) && pm is >= 100 and <= 5000)
        {
            pollMs = pm;
        }

        string fmt = string.IsNullOrWhiteSpace(UiDateTimeFormat) ? "dd/MM/yyyy HH:mm:ss" : UiDateTimeFormat.Trim();

        var snapshot = new AppSettings
        {
            ServiceBaseUrl = ServiceBaseUrl.Trim(),
            HttpTimeoutSeconds = sec,
            AutoStartWslService = AutoStartWslService,
            WslDockerServiceWindowsPath = string.IsNullOrWhiteSpace(WslDockerServiceWindowsPath)
                ? null
                : WslDockerServiceWindowsPath.Trim(),
            WslDistribution = string.IsNullOrWhiteSpace(WslDistribution) ? null : WslDistribution.Trim(),
            UiTimeZoneId = string.IsNullOrWhiteSpace(SelectedUiTimeZoneId) ? null : SelectedUiTimeZoneId.Trim(),
            UiDateTimeFormat = fmt,
            WslAutoStartHealthWaitSeconds = autoW,
            WslManualHealthWaitSeconds = manW,
            HealthProbeSingleRequestSeconds = probe,
            WslHealthPollIntervalMilliseconds = pollMs,
        };
        AppSettingsDefaults.Normalize(snapshot);
        return snapshot;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var health = await _apiClient.GetHealthAsync(_shutdownToken.Token).ConfigureAwait(true);
            _healthCache.SetFromHealthResponse(health);
            ApiResult<DockerInfoData> docker = await _apiClient.GetDockerInfoAsync(_shutdownToken.Token).ConfigureAwait(true);
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
                d = docker.Error?.Message ?? "Lỗi Docker";
            }

            StatusMessage = $"Service: {h} | {d}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy (đóng ứng dụng).";
        }
        catch (Exception ex)
        {
            _healthCache.SetFromHealthResponse(null);
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

/// <summary>
/// Một dòng trong ComboBox múi giờ (Id rỗng = giờ máy).
/// </summary>
public sealed class TimeZoneOptionItem
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;
}
