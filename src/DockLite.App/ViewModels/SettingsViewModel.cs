using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
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
    private readonly INotificationService _notificationService;

    /// <summary>
    /// Giá trị chủ đề đã lưu lần cuối (file), dùng khi Lưu để biết có đổi chủ đề không.
    /// </summary>
    private string _loadedUiTheme = "Light";

    /// <summary>
    /// Mã ngôn ngữ UI đã lưu lần cuối (vi hoặc en).
    /// </summary>
    private string _loadedUiLanguage = "vi";

    private const int ToastMessageMaxChars = 520;

    public SettingsViewModel(
        IAppSettingsStore store,
        DockLiteHttpSession httpSession,
        IDockLiteApiClient apiClient,
        string appBaseDirectory,
        AppSettings initialSettings,
        IAppShutdownToken shutdownToken,
        WslServiceHealthCache healthCache,
        AppUiDisplaySettings uiDisplay,
        INotificationService notificationService)
    {
        _store = store;
        _httpSession = httpSession;
        _apiClient = apiClient;
        _appBaseDirectory = appBaseDirectory;
        _shutdownToken = shutdownToken;
        _healthCache = healthCache;
        _uiDisplay = uiDisplay;
        _notificationService = notificationService;
        _healthCache.Changed += (_, _) => NotifyWslServiceButtonStates();
        _loadedUiTheme = initialSettings.UiTheme ?? "Light";
        ServiceBaseUrl = initialSettings.ServiceBaseUrl;
        ServiceBaseUrlSecurityHint = BuildNonLocalhostServiceUrlWarning(ServiceBaseUrl);
        ServiceBaseUrlPortHint = BuildServiceBaseUrlPortHint(ServiceBaseUrl);
        int sec = initialSettings.HttpTimeoutSeconds >= 30 ? initialSettings.HttpTimeoutSeconds : 120;
        HttpTimeoutSecondsText = sec.ToString();
        AutoStartWslService = initialSettings.AutoStartWslService;
        WslDockerServiceWindowsPath = initialSettings.WslDockerServiceWindowsPath ?? string.Empty;
        WslDockerServiceSyncSourceWindowsPath = initialSettings.WslDockerServiceSyncSourceWindowsPath ?? string.Empty;
        WslDistribution = initialSettings.WslDistribution ?? string.Empty;
        SelectedUiTimeZoneId = initialSettings.UiTimeZoneId ?? string.Empty;
        UiDateTimeFormat = string.IsNullOrWhiteSpace(initialSettings.UiDateTimeFormat)
            ? "yyyy/MM/dd HH:mm:ss"
            : initialSettings.UiDateTimeFormat;
        WslAutoStartHealthWaitSecondsText = initialSettings.WslAutoStartHealthWaitSeconds.ToString();
        WslManualHealthWaitSecondsText = initialSettings.WslManualHealthWaitSeconds.ToString();
        HealthProbeSingleRequestSecondsText = initialSettings.HealthProbeSingleRequestSeconds.ToString();
        WslHealthPollIntervalMillisecondsText = initialSettings.WslHealthPollIntervalMilliseconds.ToString();
        WslDockerServiceLinuxSyncPath = initialSettings.WslDockerServiceLinuxSyncPath ?? string.Empty;
        WslDockerServiceSyncDeleteExtra = initialSettings.WslDockerServiceSyncDeleteExtra;
        WslDockerServiceSyncEnforceVersionGe = initialSettings.WslDockerServiceSyncEnforceVersionGe;
        TimeZoneOptions.Add(new TimeZoneOptionItem { Id = "", DisplayName = "Giờ máy (Windows local)" });
        foreach (TimeZoneInfo z in TimeZoneInfo.GetSystemTimeZones().OrderBy(x => x.DisplayName))
        {
            TimeZoneOptions.Add(new TimeZoneOptionItem { Id = z.Id, DisplayName = z.DisplayName });
        }

        RebuildUiThemeTitles();

        var langProbe = new AppSettings { UiLanguage = initialSettings.UiLanguage ?? "vi" };
        AppSettingsDefaults.Normalize(langProbe);
        _loadedUiLanguage = langProbe.UiLanguage;
        RebuildUiLanguageList(_loadedUiLanguage);

        EffectiveWslPathSummary = BuildEffectiveWslPathSummary();
        UpdateDatePreview();
    }

    /// <summary>
    /// Lựa chọn chủ đề (Sáng / Tối); lưu file là Light hoặc Dark.
    /// </summary>
    public ObservableCollection<UiThemeListItem> UiThemeList { get; } = new();

    /// <summary>
    /// Lựa chọn ngôn ngữ chuỗi giao diện (vi / en).
    /// </summary>
    public ObservableCollection<UiLanguageListItem> UiLanguageList { get; } = new();

    [ObservableProperty]
    private UiThemeListItem? _selectedUiThemeItem;

    [ObservableProperty]
    private UiLanguageListItem? _selectedUiLanguageItem;

    public bool CanStartWslServiceButton => !IsBusy && _healthCache.LastHealthy != true;

    public bool CanStopWslServiceButton => !IsBusy && _healthCache.LastHealthy == true;

    public bool CanRestartWslServiceButton => !IsBusy && _healthCache.LastHealthy == true;

    /// <summary>
    /// Ảnh chụp cấu hình từ ô hiện tại (Start/Stop/Restart từ header và Cài đặt).
    /// </summary>
    public AppSettings GetSettingsSnapshotForWslCommands() => CreateSettingsSnapshotForWsl();

    [ObservableProperty]
    private string _serviceBaseUrl = string.Empty;

    /// <summary>
    /// Cảnh báo khi host base URL không phải localhost (HTTP không mã hóa trên LAN).
    /// </summary>
    [ObservableProperty]
    private string _serviceBaseUrlSecurityHint = string.Empty;

    /// <summary>
    /// Gợi ý khi cổng trong URL khác cổng mặc định của DockLite.
    /// </summary>
    [ObservableProperty]
    private string _serviceBaseUrlPortHint = string.Empty;

    [ObservableProperty]
    private bool _autoStartWslService = true;

    [ObservableProperty]
    private string _wslDockerServiceWindowsPath = string.Empty;

    /// <summary>
    /// Thư mục wsl-docker-service trên ổ Windows dùng chỉ cho đồng bộ (để trống = cùng đường dẫn với ô dịch vụ phía trên).
    /// </summary>
    [ObservableProperty]
    private string _wslDockerServiceSyncSourceWindowsPath = string.Empty;

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
    /// Kết quả kiểm tra nhanh: API + wslpath cho các ô đường dẫn (giá trị ô hiện tại).
    /// </summary>
    [ObservableProperty]
    private string _wslQuickDiagnosticsText = string.Empty;

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

    /// <summary>
    /// Đường dẫn Unix đích khi đồng bộ mã wsl-docker-service từ Windows vào filesystem Linux (WSL).
    /// </summary>
    [ObservableProperty]
    private string _wslDockerServiceLinuxSyncPath = string.Empty;

    /// <summary>
    /// Khi đồng bộ: rsync --delete (nếu có rsync trong WSL).
    /// </summary>
    [ObservableProperty]
    private bool _wslDockerServiceSyncDeleteExtra;

    /// <summary>
    /// Chỉ đồng bộ khi version trong file VERSION (Windows) >= version trên đích (WSL).
    /// </summary>
    [ObservableProperty]
    private bool _wslDockerServiceSyncEnforceVersionGe;

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

    partial void OnServiceBaseUrlChanged(string value)
    {
        ServiceBaseUrlSecurityHint = BuildNonLocalhostServiceUrlWarning(value);
        ServiceBaseUrlPortHint = BuildServiceBaseUrlPortHint(value);
    }

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
        string previewPrefix = UiLanguageManager.TryLocalize(
            Application.Current,
            "Ui_Settings_Display_DatePreviewPrefix",
            "UTC hiện tại → ");
        DateFormatPreviewText = previewPrefix + _uiDisplay.PreviewFormatUtc(
            DateTime.UtcNow,
            string.IsNullOrWhiteSpace(SelectedUiTimeZoneId) ? null : SelectedUiTimeZoneId,
            UiDateTimeFormat);
    }

    /// <summary>
    /// Nạp lại nhãn ComboBox ngôn ngữ theo từ điển hiện tại và chọn dòng theo <paramref name="selectedId"/>.
    /// </summary>
    private void RebuildUiLanguageList(string selectedId)
    {
        Application? app = Application.Current;
        string viTitle = "Tiếng Việt";
        string enTitle = "English";
        if (app is not null)
        {
            viTitle = UiLanguageManager.FindString(app, "Ui_Lang_Vi");
            enTitle = UiLanguageManager.FindString(app, "Ui_Lang_En");
        }

        UiLanguageList.Clear();
        UiLanguageList.Add(new UiLanguageListItem { Id = "vi", Title = viTitle });
        UiLanguageList.Add(new UiLanguageListItem { Id = "en", Title = enTitle });
        SelectedUiLanguageItem = UiLanguageList.FirstOrDefault(x => x.Id == selectedId) ?? UiLanguageList[0];
    }

    /// <summary>
    /// Nạp lại nhãn ComboBox chủ đề (Sáng/Tối) theo từ điển hiện tại và giữ đúng <see cref="_loadedUiTheme"/>.
    /// </summary>
    private void RebuildUiThemeTitles()
    {
        Application? app = Application.Current;
        string lightTitle = "Sáng";
        string darkTitle = "Tối";
        if (app is not null)
        {
            lightTitle = UiLanguageManager.FindString(app, "Ui_Settings_Theme_Light");
            darkTitle = UiLanguageManager.FindString(app, "Ui_Settings_Theme_Dark");
        }

        UiThemeList.Clear();
        UiThemeList.Add(new UiThemeListItem { Id = "Light", Title = lightTitle });
        UiThemeList.Add(new UiThemeListItem { Id = "Dark", Title = darkTitle });
        SelectedUiThemeItem = UiThemeList.FirstOrDefault(x => x.Id == _loadedUiTheme) ?? UiThemeList[0];
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

    /// <summary>
    /// Gọi health + Docker info và thử wslpath cho các ô đường dẫn WSL (không bắt buộc Lưu trước).
    /// </summary>
    [RelayCommand]
    private async Task QuickWslDiagnosticsAsync()
    {
        IsBusy = true;
        WslQuickDiagnosticsText = string.Empty;
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== API (ô Địa chỉ base URL phía trên) ===");
            HealthResponse? health = await _apiClient.GetHealthAsync(_shutdownToken.Token).ConfigureAwait(true);
            _healthCache.SetFromHealthResponse(health, forceNotify: true);
            ApiResult<DockerInfoData> docker = await _apiClient.GetDockerInfoAsync(_shutdownToken.Token).ConfigureAwait(true);
            string h = health is null
                ? "—"
                : $"{health.Service} ({health.Status}) — version {health.Version}";
            sb.Append("Service: ").AppendLine(h);
            if (docker.Success && docker.Data is not null)
            {
                string ver = docker.Data.ServerVersion ?? "?";
                string os = docker.Data.OperatingSystem ?? docker.Data.OsType ?? "?";
                sb.Append("Docker Engine: ").Append(ver).Append(" (").Append(os).AppendLine(")");
            }
            else
            {
                sb.Append("Docker: ").AppendLine(docker.Error?.Message ?? "Lỗi Docker");
            }

            sb.AppendLine();
            sb.AppendLine("=== wslpath (tab WSL — dùng giá trị ô hiện tại, chưa Lưu vẫn áp dụng) ===");
            string? distro = string.IsNullOrWhiteSpace(WslDistribution) ? null : WslDistribution.Trim();

            AppendDiagnosticsPath(sb, "Thư mục dịch vụ", WslDockerServiceWindowsPath, distro, unixOnly: false);
            AppendDiagnosticsPath(sb, "Nguồn đồng bộ", WslDockerServiceSyncSourceWindowsPath, distro, unixOnly: false);
            AppendDiagnosticsPath(sb, "Đích đồng bộ (Unix)", WslDockerServiceLinuxSyncPath, distro, unixOnly: true);

            WslQuickDiagnosticsText = sb.ToString().TrimEnd();
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_QuickDiagDone", "Đã chạy kiểm tra nhanh (xem khối kết quả bên dưới).");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_CancelledAppExit", "Đã hủy (đóng ứng dụng).");
        }
        catch (Exception ex)
        {
            _healthCache.SetFromHealthResponse(null, forceNotify: true);
            AppFileLog.WriteException("Kiểm tra nhanh WSL", ex);
            WslQuickDiagnosticsText = ExceptionMessages.FormatForUser(ex);
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_QuickDiagError", "Kiểm tra nhanh gặp lỗi (xem khối kết quả).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void AppendDiagnosticsPath(StringBuilder sb, string label, string path, string? distro, bool unixOnly)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            sb.Append(label).AppendLine(": (trống)");
            sb.AppendLine();
            return;
        }

        string t = path.Trim();
        if (unixOnly)
        {
            sb.Append(label).Append(": ").AppendLine(t);
            sb.AppendLine("  (đường Unix — không qua wslpath)");
            sb.AppendLine();
            return;
        }

        if (WslPathProbe.TryWindowsToUnix(t, distro, out string unix, out string? err))
        {
            sb.Append(label).Append(": ").AppendLine(t);
            sb.Append("  → Unix: ").AppendLine(unix);
        }
        else
        {
            sb.Append(label).Append(": ").AppendLine(t);
            sb.Append("  → Lỗi: ").AppendLine(err ?? "?");
        }

        sb.AppendLine();
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

        string? distro = string.IsNullOrWhiteSpace(WslDistribution) ? null : WslDistribution.Trim();

        string path = WslDockerServiceWindowsPath.Trim();
        if (string.IsNullOrEmpty(path))
        {
            sb.AppendLine("Thư mục dịch vụ (nhìn từ Windows): (trống — DockLite tự tìm wsl-docker-service cạnh file chạy ứng dụng).");
        }
        else
        {
            if (WslPathProbe.TryWindowsToUnix(path, distro, out string unix, out string? err))
            {
                sb.Append("Thư mục dịch vụ (Windows): ").AppendLine(path);
                sb.Append("→ trong WSL: ").AppendLine(unix);
            }
            else
            {
                sb.Append("Thư mục dịch vụ (Windows): ").AppendLine(path);
                sb.Append("→ wslpath: ").AppendLine(err ?? "lỗi");
            }
        }

        string syncSrc = WslDockerServiceSyncSourceWindowsPath.Trim();
        if (!string.IsNullOrEmpty(syncSrc))
        {
            sb.AppendLine();
            sb.AppendLine("Nguồn đồng bộ (Windows):");
            if (WslPathProbe.TryWindowsToUnix(syncSrc, distro, out string unixSrc, out string? errS))
            {
                sb.AppendLine(syncSrc);
                sb.Append("→ Unix: ").AppendLine(unixSrc);
            }
            else
            {
                sb.AppendLine(syncSrc);
                sb.Append("→ wslpath: ").AppendLine(errS ?? "lỗi");
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
            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Settings_Status_WslIpFilledFormat",
                "Đã điền địa chỉ theo IP WSL ({0}). Nhấn Lưu để áp dụng cho toàn bộ ứng dụng.",
                ip);
            return;
        }

        StatusMessage = UiLanguageManager.TryLocalizeCurrent(
            "Ui_Settings_Status_WslIpLookupFailed",
            "Không lấy được IP WSL (wsl hostname -I). Kiểm tra WSL đã bật; nếu có nhiều distro, điền tên Distro WSL rồi thử lại.");
    }

    [RelayCommand]
    private void Save()
    {
        if (!int.TryParse(HttpTimeoutSecondsText.Trim(), out int sec) || sec < 30 || sec > 600)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_ValidationHttpTimeout", "Timeout phải là số nguyên từ 30 đến 600 giây.");
            return;
        }

        if (!int.TryParse(WslAutoStartHealthWaitSecondsText.Trim(), out int autoW) || autoW < 10 || autoW > 600)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_ValidationAutoHealthWait", "Chờ health khi tự khởi động WSL: số nguyên từ 10 đến 600 giây.");
            return;
        }

        if (!int.TryParse(WslManualHealthWaitSecondsText.Trim(), out int manW) || manW < 10 || manW > 600)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_ValidationManualHealthWait", "Chờ health khi Start/Restart thủ công: số nguyên từ 10 đến 600 giây.");
            return;
        }

        if (!int.TryParse(HealthProbeSingleRequestSecondsText.Trim(), out int probe) || probe < 1 || probe > 60)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_ValidationHealthProbe", "Timeout một lần gọi /api/health: từ 1 đến 60 giây.");
            return;
        }

        if (!int.TryParse(WslHealthPollIntervalMillisecondsText.Trim(), out int pollMs) || pollMs < 100 || pollMs > 5000)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_ValidationHealthPollMs", "Khoảng cách poll health: từ 100 đến 5000 ms.");
            return;
        }

        if (string.IsNullOrWhiteSpace(UiDateTimeFormat))
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_ValidationDateFormatEmpty", "Định dạng ngày giờ không được để trống.");
            return;
        }

        if (SelectedUiThemeItem is null)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_SelectTheme", "Chọn chủ đề (Sáng hoặc Tối).");
            return;
        }

        if (SelectedUiLanguageItem is null)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_SelectLanguage", "Chọn ngôn ngữ giao diện.");
            return;
        }

        string themeBeforeSave = _loadedUiTheme;
        string langBeforeSave = _loadedUiLanguage;
        var settings = new AppSettings
        {
            ServiceBaseUrl = ServiceBaseUrl.Trim(),
            HttpTimeoutSeconds = sec,
            AutoStartWslService = AutoStartWslService,
            WslDockerServiceWindowsPath = string.IsNullOrWhiteSpace(WslDockerServiceWindowsPath)
                ? null
                : WslDockerServiceWindowsPath.Trim(),
            WslDockerServiceSyncSourceWindowsPath = string.IsNullOrWhiteSpace(WslDockerServiceSyncSourceWindowsPath)
                ? null
                : WslDockerServiceSyncSourceWindowsPath.Trim(),
            WslDistribution = string.IsNullOrWhiteSpace(WslDistribution) ? null : WslDistribution.Trim(),
            UiTimeZoneId = string.IsNullOrWhiteSpace(SelectedUiTimeZoneId) ? null : SelectedUiTimeZoneId.Trim(),
            UiDateTimeFormat = UiDateTimeFormat.Trim(),
            UiTheme = SelectedUiThemeItem.Id,
            UiLanguage = SelectedUiLanguageItem.Id,
            WslAutoStartHealthWaitSeconds = autoW,
            WslManualHealthWaitSeconds = manW,
            HealthProbeSingleRequestSeconds = probe,
            WslHealthPollIntervalMilliseconds = pollMs,
            WslDockerServiceLinuxSyncPath = string.IsNullOrWhiteSpace(WslDockerServiceLinuxSyncPath)
                ? null
                : WslDockerServiceLinuxSyncPath.Trim(),
            WslDockerServiceSyncDeleteExtra = WslDockerServiceSyncDeleteExtra,
            WslDockerServiceSyncEnforceVersionGe = WslDockerServiceSyncEnforceVersionGe,
        };
        AppSettingsDefaults.Normalize(settings);
        _store.Save(settings);
        _httpSession.Reconfigure(settings);
        _uiDisplay.Apply(settings);
        ThemeManager.Apply(Application.Current, settings);
        UiLanguageManager.Apply(Application.Current, settings);
        _loadedUiTheme = settings.UiTheme;
        _loadedUiLanguage = settings.UiLanguage;
        RebuildUiThemeTitles();
        RebuildUiLanguageList(_loadedUiLanguage);
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
            "Đã lưu. Đích: " + hostSummary + ", timeout=" + sec + "s, tự khởi động WSL=" + settings.AutoStartWslService + ", chủ đề=" + settings.UiTheme);
        StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_SavedApply", "Đã lưu. Địa chỉ, timeout, hiển thị thời gian, chủ đề, ngôn ngữ vỏ cửa sổ và chờ health đã áp dụng.");
        if (!string.Equals(themeBeforeSave, settings.UiTheme, StringComparison.Ordinal))
        {
            StatusMessage += UiLanguageManager.TryLocalizeCurrent(
                "Ui_Settings_Status_SavedThemeRestartHint",
                " Chủ đề: đã cập nhật từ điển tài nguyên; nếu một số màn hình vẫn chưa đổi màu, hãy khởi động lại ứng dụng.");
        }

        if (!string.Equals(langBeforeSave, settings.UiLanguage, StringComparison.Ordinal))
        {
            StatusMessage += UiLanguageManager.TryLocalizeCurrent(
                "Ui_Settings_Status_SavedLanguagePartialI18n",
                " Ngôn ngữ: đã cập nhật từ điển giao diện; thông báo trạng thái và nhật ký nội dung có thể vẫn tiếng Việt cho đến khi bổ sung đầy đủ bản dịch.");
        }

        EffectiveWslPathSummary = BuildEffectiveWslPathSummary();
        UpdateDatePreview();
    }

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
            StatusMessage = msg;
            AppFileLog.Write("WSL thủ công", msg + (sent && healthOk ? " [health OK]" : ""));
            await _healthCache.RefreshAsync(_apiClient, _shutdownToken.Token, forceNotify: true).ConfigureAwait(true);
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
                StatusMessage = msg;
                await _healthCache.RefreshAsync(_apiClient, _shutdownToken.Token, forceNotify: true).ConfigureAwait(true);
                return;
            }

            StatusMessage = msg;
            await Task.Delay(800, _shutdownToken.Token).ConfigureAwait(true);
            await _healthCache.RefreshAsync(_apiClient, _shutdownToken.Token, forceNotify: true).ConfigureAwait(true);
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
            StatusMessage = msg;
            AppFileLog.Write("WSL restart thủ công", msg + (sent && healthOk ? " [health OK]" : ""));
            await _healthCache.RefreshAsync(_apiClient, _shutdownToken.Token, forceNotify: true).ConfigureAwait(true);
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
                StatusMessage = msg;
                return;
            }

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
            WslDockerServiceSyncSourceWindowsPath = string.IsNullOrWhiteSpace(WslDockerServiceSyncSourceWindowsPath)
                ? null
                : WslDockerServiceSyncSourceWindowsPath.Trim(),
            WslDistribution = string.IsNullOrWhiteSpace(WslDistribution) ? null : WslDistribution.Trim(),
            UiTimeZoneId = string.IsNullOrWhiteSpace(SelectedUiTimeZoneId) ? null : SelectedUiTimeZoneId.Trim(),
            UiDateTimeFormat = fmt,
            UiTheme = SelectedUiThemeItem?.Id ?? "Light",
            UiLanguage = SelectedUiLanguageItem?.Id ?? "vi",
            WslAutoStartHealthWaitSeconds = autoW,
            WslManualHealthWaitSeconds = manW,
            HealthProbeSingleRequestSeconds = probe,
            WslHealthPollIntervalMilliseconds = pollMs,
            WslDockerServiceLinuxSyncPath = string.IsNullOrWhiteSpace(WslDockerServiceLinuxSyncPath)
                ? null
                : WslDockerServiceLinuxSyncPath.Trim(),
            WslDockerServiceSyncDeleteExtra = WslDockerServiceSyncDeleteExtra,
            WslDockerServiceSyncEnforceVersionGe = WslDockerServiceSyncEnforceVersionGe,
        };
        AppSettingsDefaults.Normalize(snapshot);
        return snapshot;
    }

    /// <summary>
    /// Sao chép mã từ ô «Nguồn trong Windows» (hoặc cùng thư mục dịch vụ nếu để trống) sang đích Unix.
    /// </summary>
    [RelayCommand]
    private async Task SyncServiceSourceToWslAsync()
    {
        IsBusy = true;
        StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_SyncSourceProgress", "Đang đồng bộ mã nguồn vào WSL...");
        try
        {
            AppSettings snapshot = CreateSettingsSnapshotForWsl();
            (bool ok, string msg) = await WslDockerServiceAutoStart
                .TrySyncWindowsSourceToLinuxDestinationAsync(snapshot, _appBaseDirectory, _shutdownToken.Token)
                .ConfigureAwait(true);
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
        try
        {
            var health = await _apiClient.GetHealthAsync(_shutdownToken.Token).ConfigureAwait(true);
            _healthCache.SetFromHealthResponse(health, forceNotify: true);
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
                d = docker.Error?.Message ?? UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_DockerErrorGeneric", "Lỗi Docker");
            }

            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent("Ui_Settings_Status_TestConnectionOkFormat", "Service: {0} | {1}", h, d);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_CancelledAppExit", "Đã hủy (đóng ứng dụng).");
        }
        catch (Exception ex)
        {
            _healthCache.SetFromHealthResponse(null, forceNotify: true);
            AppFileLog.WriteException("Kiểm tra kết nối", ex);
            string msg = ExceptionMessages.FormatForUser(ex);
            if (ex is System.Net.Http.HttpRequestException)
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

    private static string BuildNonLocalhostServiceUrlWarning(string? serviceBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(serviceBaseUrl))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(serviceBaseUrl.Trim(), UriKind.Absolute, out Uri? u))
        {
            return string.Empty;
        }

        string host = u.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host == "127.0.0.1"
            || host == "::1"
            || host.StartsWith("[::1]", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return "Host không phải localhost (127.0.0.1 / ::1): HTTP trên mạng LAN có thể bị nghe lén (MITM). Chỉ dùng khi bạn tin cậy mạng hoặc có bảo vệ tương đương.";
    }

    private static string BuildServiceBaseUrlPortHint(string? serviceBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(serviceBaseUrl))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(serviceBaseUrl.Trim(), UriKind.Absolute, out Uri? u))
        {
            return string.Empty;
        }

        int port = u.Port;
        if (port == DockLiteApiDefaults.DefaultPort)
        {
            return $"Cổng {DockLiteApiDefaults.DefaultPort} là mặc định. Nếu cổng bị ứng dụng khác chiếm: trong WSL khởi động service với biến môi trường DOCKLITE_ADDR (ví dụ 0.0.0.0:17891) và đặt Base URL trùng cổng đó.";
        }

        return $"Cổng trong URL là {port} (mặc định DockLite là {DockLiteApiDefaults.DefaultPort}). Đảm bảo tiến trình wsl-docker-service trong WSL đang lắng nghe đúng cổng này.";
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

/// <summary>
/// Một dòng trong ComboBox chủ đề (Light / Dark).
/// </summary>
public sealed class UiThemeListItem
{
    public required string Id { get; init; }

    public required string Title { get; init; }
}

/// <summary>
/// Một dòng trong ComboBox ngôn ngữ giao diện (vi / en).
/// </summary>
public sealed class UiLanguageListItem
{
    public required string Id { get; init; }

    public required string Title { get; init; }
}
