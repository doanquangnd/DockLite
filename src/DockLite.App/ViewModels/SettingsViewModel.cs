using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Help;
using DockLite.App.Services;
using DockLite.Contracts.Api;
using DockLite.Core;
using DockLite.Core.Configuration;
using DockLite.Core.Diagnostics;
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
    private readonly ISystemDiagnosticsScreenApi _systemDiagnosticsApi;
    private readonly string _appBaseDirectory;
    private readonly IAppShutdownToken _shutdownToken;
    private readonly WslServiceHealthCache _healthCache;
    private readonly AppUiDisplaySettings _uiDisplay;
    private readonly INotificationService _notificationService;

    /// <summary>
    /// Đường dẫn tới <c>docs/docklite-lan-security.md</c> khi có trong tree (dev); null nếu không tìm thấy.
    /// </summary>
    private readonly string? _lanSecurityMarkdownPath;

    /// <summary>
    /// Giá trị chủ đề đã lưu lần cuối (file), dùng khi Lưu để biết có đổi chủ đề không.
    /// </summary>
    private string _loadedUiTheme = "Light";

    /// <summary>
    /// Mã ngôn ngữ UI đã lưu lần cuối (vi hoặc en).
    /// </summary>
    private string _loadedUiLanguage = "vi";

    private const int ToastMessageMaxChars = 520;

    private readonly SemaphoreSlim _saveSettingsGate = new(1, 1);

    public SettingsViewModel(
        IAppSettingsStore store,
        DockLiteHttpSession httpSession,
        ISystemDiagnosticsScreenApi systemDiagnosticsApi,
        string appBaseDirectory,
        AppSettings initialSettings,
        IAppShutdownToken shutdownToken,
        WslServiceHealthCache healthCache,
        AppUiDisplaySettings uiDisplay,
        INotificationService notificationService)
    {
        _store = store;
        _httpSession = httpSession;
        _systemDiagnosticsApi = systemDiagnosticsApi;
        _appBaseDirectory = appBaseDirectory;
        _shutdownToken = shutdownToken;
        _healthCache = healthCache;
        _uiDisplay = uiDisplay;
        _notificationService = notificationService;
        _lanSecurityMarkdownPath = LanSecurityDocPaths.TryResolve(_appBaseDirectory);
        _healthCache.Changed += (_, _) => NotifyWslServiceButtonStates();
        _loadedUiTheme = initialSettings.UiTheme ?? "Light";
        ServiceBaseUrl = initialSettings.ServiceBaseUrl;
        ServiceApiToken = initialSettings.ServiceApiToken ?? string.Empty;
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
        ContainerStatsCpuWarnPercentText = initialSettings.ContainerStatsCpuWarnPercent.ToString(CultureInfo.InvariantCulture);
        ContainerStatsMemoryWarnPercentText = initialSettings.ContainerStatsMemoryWarnPercent.ToString(CultureInfo.InvariantCulture);
        WslAutoStartHealthWaitSecondsText = initialSettings.WslAutoStartHealthWaitSeconds.ToString();
        WslManualHealthWaitSecondsText = initialSettings.WslManualHealthWaitSeconds.ToString();
        HealthProbeSingleRequestSecondsText = initialSettings.HealthProbeSingleRequestSeconds.ToString();
        WslHealthPollIntervalMillisecondsText = initialSettings.WslHealthPollIntervalMilliseconds.ToString();
        WslDockerServiceLinuxSyncPath = initialSettings.WslDockerServiceLinuxSyncPath ?? string.Empty;
        WslDockerServiceSyncDeleteExtra = initialSettings.WslDockerServiceSyncDeleteExtra;
        WslDockerServiceSyncEnforceVersionGe = initialSettings.WslDockerServiceSyncEnforceVersionGe;
        DiagnosticLocalTelemetryEnabled = initialSettings.DiagnosticLocalTelemetryEnabled;
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

        SettingsFilePathDisplay = _store.SettingsFilePath;
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
    /// Có tệp <c>docs/docklite-lan-security.md</c> trên đĩa (thường gặp khi build từ source).
    /// </summary>
    public bool LanSecurityDocOpenEnabled =>
        !string.IsNullOrEmpty(_lanSecurityMarkdownPath) && File.Exists(_lanSecurityMarkdownPath);

    /// <summary>
    /// Ảnh chụp cấu hình từ ô hiện tại (Start/Stop/Restart từ header và Cài đặt).
    /// </summary>
    public AppSettings GetSettingsSnapshotForWslCommands() => CreateSettingsSnapshotForWsl();

    /// <summary>
    /// Tab Cài đặt đang chọn (0 = Kết nối). Dùng khi mở từ banner mất kết nối.
    /// </summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _serviceBaseUrl = string.Empty;

    /// <summary>
    /// Token API (Bearer) khớp với DOCKLITE_API_TOKEN trên service; để trống nếu không bật xác thực.
    /// </summary>
    [ObservableProperty]
    private string _serviceApiToken = string.Empty;

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

    /// <summary>
    /// Đường dẫn file <c>settings.json</c> trong %LocalAppData% (hiển thị và sao lưu).
    /// </summary>
    [ObservableProperty]
    private string _settingsFilePathDisplay = string.Empty;

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
    /// Đang ghi file cài đặt (Lưu) — tránh double-submit khi bấm nhanh nhiều lần.
    /// </summary>
    [ObservableProperty]
    private bool _isSavingSettings;

    /// <summary>
    /// Cho phép bấm nút trên trang Cài đặt (không <see cref="IsBusy"/> và không đang Lưu).
    /// </summary>
    public bool CanInteractFooter => !IsBusy && !IsSavingSettings;

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
    /// Snapshot tài nguyên Linux phía service (GET /api/wsl/host-resources).
    /// </summary>
    [ObservableProperty]
    private string _wslResourcesText = string.Empty;

    [ObservableProperty]
    private bool _isWslResourcesBusy;

    /// <summary>
    /// Nút làm mới tài nguyên WSL: không trùng với thao tác Lưu đang bận.
    /// </summary>
    public bool CanRefreshWslResources => CanInteractFooter && !IsWslResourcesBusy;

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
    /// Ngưỡng CPU % cảnh báo trên màn Container (0–100, 0 = tắt); parse khi Lưu.
    /// </summary>
    [ObservableProperty]
    private string _containerStatsCpuWarnPercentText = "0";

    /// <summary>
    /// Ngưỡng RAM % (dùng/giới hạn) cảnh báo (0–100, 0 = tắt); parse khi Lưu.
    /// </summary>
    [ObservableProperty]
    private string _containerStatsMemoryWarnPercentText = "0";

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

    /// <summary>
    /// Ghi file chẩn đoán cục bộ (opt-in), cùng thư mục log ứng dụng.
    /// </summary>
    [ObservableProperty]
    private bool _diagnosticLocalTelemetryEnabled;

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
        string systemTitle = "Theo hệ thống";
        if (app is not null)
        {
            lightTitle = UiLanguageManager.FindString(app, "Ui_Settings_Theme_Light");
            darkTitle = UiLanguageManager.FindString(app, "Ui_Settings_Theme_Dark");
            systemTitle = UiLanguageManager.FindString(app, "Ui_Settings_Theme_System");
        }

        UiThemeList.Clear();
        UiThemeList.Add(new UiThemeListItem { Id = "Light", Title = lightTitle });
        UiThemeList.Add(new UiThemeListItem { Id = "Dark", Title = darkTitle });
        UiThemeList.Add(new UiThemeListItem { Id = "System", Title = systemTitle });
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
        OnPropertyChanged(nameof(CanInteractFooter));
        OnPropertyChanged(nameof(CanRefreshWslResources));
    }

    partial void OnIsSavingSettingsChanged(bool value)
    {
        OnPropertyChanged(nameof(CanInteractFooter));
        OnPropertyChanged(nameof(CanRefreshWslResources));
    }

    partial void OnIsWslResourcesBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRefreshWslResources));
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
    /// Sao chép file cài đặt (hoặc ghi JSON mặc định nếu chưa từng Lưu) ra file do người dùng chọn.
    /// </summary>
    [RelayCommand]
    private void ExportSettingsToFile()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            FileName = "docklite-settings-backup.json",
            AddExtension = true,
        };

        if (dlg.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _store.ExportToCopy(dlg.FileName);
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Settings_Status_ExportSettingsDone",
                "Đã sao lưu file cài đặt.");
            AppFileLog.Write("Cài đặt", "Export settings → " + dlg.FileName);
        }
        catch (Exception ex)
        {
            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Settings_Status_ExportSettingsErrorFormat",
                "Không sao lưu được: {0}",
                ex.Message);
            AppFileLog.WriteException("Export settings", ex);
        }
    }

    /// <summary>
    /// Mở tài liệu bảo mật LAN (Markdown) bằng ứng dụng mặc định của hệ thống.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOpenLanSecurityDoc))]
    private void OpenLanSecurityDoc()
    {
        if (string.IsNullOrWhiteSpace(_lanSecurityMarkdownPath) || !File.Exists(_lanSecurityMarkdownPath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_lanSecurityMarkdownPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppFileLog.WriteException("Mở docklite-lan-security.md", ex);
        }
    }

    private bool CanOpenLanSecurityDoc() => LanSecurityDocOpenEnabled;

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
            sb.AppendLine(BuildRecoveryChecklistPreamble());
            sb.AppendLine();
            sb.AppendLine("=== API (ô Địa chỉ base URL phía trên) ===");
            HealthResponse? health = await _systemDiagnosticsApi.GetHealthAsync(_shutdownToken.Token).ConfigureAwait(true);
            ApiResult<DockerInfoData> docker = await _systemDiagnosticsApi.GetDockerInfoAsync(_shutdownToken.Token).ConfigureAwait(true);
            bool connectivityOk = health is not null && docker.Success && docker.Data is not null;
            if (connectivityOk)
            {
                _healthCache.SetFromHealthResponse(health!, forceNotify: true);
            }
            else
            {
                _healthCache.SetFromHealthResponse(null, forceNotify: true);
            }
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
            sb.AppendLine("=== uname (WSL distro) ===");
            string? distroForUname = string.IsNullOrWhiteSpace(WslDistribution) ? null : WslDistribution.Trim();
            (bool uOk, string uOut, string? uErr) = await Task.Run(() =>
            {
                if (WslDistroProbe.TryRunUname(distroForUname, out string so, out string? e))
                {
                    return (true, so, (string?)null);
                }

                return (false, string.Empty, e);
            }).ConfigureAwait(true);
            if (uOk)
            {
                sb.AppendLine(uOut.TrimEnd());
            }
            else
            {
                sb.AppendLine(uErr ?? "Không chạy được uname trong WSL.");
            }

            sb.AppendLine();
            sb.AppendLine("=== wslpath (tab WSL — dùng giá trị ô hiện tại, chưa Lưu vẫn áp dụng) ===");
            string? distro = string.IsNullOrWhiteSpace(WslDistribution) ? null : WslDistribution.Trim();

            AppendDiagnosticsPath(sb, "Thư mục dịch vụ", WslDockerServiceWindowsPath, distro, unixOnly: false);
            AppendDiagnosticsPath(sb, "Nguồn đồng bộ", WslDockerServiceSyncSourceWindowsPath, distro, unixOnly: false);
            AppendDiagnosticsPath(sb, "Đích đồng bộ (Unix)", WslDockerServiceLinuxSyncPath, distro, unixOnly: true);

            WslQuickDiagnosticsText = sb.ToString().TrimEnd();
            EffectiveWslPathSummary = BuildEffectiveWslPathSummary();
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
            WslQuickDiagnosticsText = NetworkErrorMessageMapper.FormatForUser(ex);
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Settings_Status_QuickDiagError", "Kiểm tra nhanh gặp lỗi (xem khối kết quả).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Checklist khôi phục kết nối (đồng bộ với header / Tổng quan: cần health + Docker).
    /// </summary>
    private static string BuildRecoveryChecklistPreamble()
    {
        var sb = new StringBuilder();
        sb.AppendLine(UiLanguageManager.TryLocalizeCurrent(
            "Ui_Settings_Diag_ChecklistTitle",
            "=== Checklist khi mất kết nối ==="));
        sb.AppendLine(UiLanguageManager.TryLocalizeCurrent(
            "Ui_Settings_Diag_ChecklistLine1",
            "1. Địa chỉ: base URL đúng (tab này — thử «Điền IP WSL» nếu 127.0.0.1 không ổn trên WSL2)."));
        sb.AppendLine(UiLanguageManager.TryLocalizeCurrent(
            "Ui_Settings_Diag_ChecklistLine2",
            "2. Token API: nếu service dùng DOCKLITE_API_TOKEN, ô token khớp rồi Lưu."));
        sb.AppendLine(UiLanguageManager.TryLocalizeCurrent(
            "Ui_Settings_Diag_ChecklistLine3",
            "3. Service Go + Docker Engine: khối «API» bên dưới phải có health và Docker (cùng điều kiện với thanh trên header)."));
        sb.AppendLine(UiLanguageManager.TryLocalizeCurrent(
            "Ui_Settings_Diag_ChecklistLine4",
            "4. WSL: distro đúng; uname và wslpath ở các khối dưới."));
        sb.AppendLine(UiLanguageManager.TryLocalizeCurrent(
            "Ui_Settings_Diag_ChecklistLine5",
            "5. Docker Engine: trong WSL (distro chạy service), daemon Docker phải sẵn sàng (Docker Desktop bật hoặc dockerd/socket đã cấu hình); có thể thử «docker info» trong distro đó."));
        return sb.ToString().TrimEnd();
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
    private async Task Save()
    {
        if (!await _saveSettingsGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        IsSavingSettings = true;
        try
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

            if (!int.TryParse(ContainerStatsCpuWarnPercentText.Trim(), out int cpuWarnPct) || cpuWarnPct < 0 || cpuWarnPct > 100)
            {
                StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Settings_Status_ValidationStatsCpuWarn",
                    "Ngưỡng cảnh báo CPU phải là số nguyên từ 0 đến 100 (0 = tắt).");
                return;
            }

            if (!int.TryParse(ContainerStatsMemoryWarnPercentText.Trim(), out int memWarnPct) || memWarnPct < 0 || memWarnPct > 100)
            {
                StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Settings_Status_ValidationStatsMemWarn",
                    "Ngưỡng cảnh báo RAM phải là số nguyên từ 0 đến 100 (0 = tắt).");
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
                ServiceApiToken = string.IsNullOrWhiteSpace(ServiceApiToken) ? null : ServiceApiToken.Trim(),
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
                DiagnosticLocalTelemetryEnabled = DiagnosticLocalTelemetryEnabled,
                ContainerStatsCpuWarnPercent = cpuWarnPct,
                ContainerStatsMemoryWarnPercent = memWarnPct,
            };
            AppSettingsDefaults.Normalize(settings);
            _store.Save(settings);
            DiagnosticTelemetry.SetEnabled(settings.DiagnosticLocalTelemetryEnabled);
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
            await PostSaveHealthProbeAsync().ConfigureAwait(true);
        }
        finally
        {
            IsSavingSettings = false;
            _saveSettingsGate.Release();
        }
    }

    /// <summary>
    /// Một lần gọi health + Docker sau khi Lưu (HttpClient đã Reconfigure) — cùng tiêu chí với header; toast nếu chưa đạt.
    /// </summary>
    private async Task PostSaveHealthProbeAsync()
    {
        try
        {
            Task<HealthResponse?> healthTask = _systemDiagnosticsApi.GetHealthAsync(_shutdownToken.Token);
            Task<ApiResult<DockerInfoData>> dockerTask = _systemDiagnosticsApi.GetDockerInfoAsync(_shutdownToken.Token);
            await Task.WhenAll(healthTask, dockerTask).ConfigureAwait(true);
            HealthResponse? health = await healthTask.ConfigureAwait(true);
            ApiResult<DockerInfoData> docker = await dockerTask.ConfigureAwait(true);
            bool connectivityOk = health is not null && docker.Success && docker.Data is not null;
            if (connectivityOk)
            {
                _healthCache.SetFromHealthResponse(health!, forceNotify: true);
                return;
            }

            _healthCache.SetFromHealthResponse(null, forceNotify: true);
            string body = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Settings_Status_PostSaveConnectivityFail",
                "Sau khi Lưu: chưa đạt kết nối đầy đủ (health + Docker Engine). Kiểm tra service WSL hoặc «Kiểm tra kết nối».");
            StatusMessage = string.IsNullOrWhiteSpace(StatusMessage) ? body : StatusMessage + " " + body;
            await _notificationService
                .ShowAsync(
                    UiLanguageManager.TryLocalizeCurrent("Ui_Toast_PostSaveHealthTitle", "Cài đặt"),
                    body,
                    NotificationDisplayKind.Warning,
                    _shutdownToken.Token)
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Đóng app.
        }
        catch (Exception ex)
        {
            _healthCache.SetFromHealthResponse(null, forceNotify: true);
            string body = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Settings_Status_PostSaveConnectivityFail",
                "Sau khi Lưu: chưa đạt kết nối đầy đủ (health + Docker Engine). Kiểm tra service WSL hoặc «Kiểm tra kết nối».");
            StatusMessage = string.IsNullOrWhiteSpace(StatusMessage)
                ? body + " " + NetworkErrorMessageMapper.FormatForUser(ex)
                : StatusMessage + " " + body;
            await ApiErrorUiFeedback.ShowNetworkExceptionToastAsync(_notificationService, ex).ConfigureAwait(true);
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
            ServiceApiToken = string.IsNullOrWhiteSpace(ServiceApiToken) ? null : ServiceApiToken.Trim(),
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
            DiagnosticLocalTelemetryEnabled = DiagnosticLocalTelemetryEnabled,
            ContainerStatsCpuWarnPercent = int.TryParse(ContainerStatsCpuWarnPercentText.Trim(), out int cw) && cw is >= 0 and <= 100
                ? cw
                : 0,
            ContainerStatsMemoryWarnPercent = int.TryParse(ContainerStatsMemoryWarnPercentText.Trim(), out int memWarn) && memWarn is >= 0 and <= 100
                ? memWarn
                : 0,
        };
        AppSettingsDefaults.Normalize(snapshot);
        return snapshot;
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
