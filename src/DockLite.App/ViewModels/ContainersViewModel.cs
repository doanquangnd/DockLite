using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Models;
using DockLite.App.Services;
using DockLite.Contracts.Api;
using DockLite.Core.Compose;
using DockLite.Core.Configuration;
using DockLite.Core.Services;

namespace DockLite.App.ViewModels;

/// <summary>
/// Danh sách container: làm mới, lọc, tìm, thao tác start/stop/restart/xóa.
/// </summary>
public partial class ContainersViewModel : ObservableObject
{
    private readonly IContainerScreenApi _containerApi;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly IAppShutdownToken _shutdownToken;
    private readonly AppShellActivityState _shellActivity;
    private readonly IAppSettingsStore _appSettingsStore;
    private List<ContainerSummaryDto> _allItems = new();
    private DispatcherTimer? _statsRealtimeTimer;
    private readonly SemaphoreSlim _statsRealtimeGate = new(1, 1);
    private readonly IStatsStreamClient _statsStream;
    private CancellationTokenSource? _statsWsCts;
    private readonly List<double> _cpuSparkHistory = new();
    private readonly List<double> _memorySparkHistory = new();
    private double _lastCpuForWarn;
    private double _lastMemPctForWarn;
    private int _statsCpuWarnPercent;
    private int _statsMemWarnPercent;
    private readonly SearchDebounceHelper _searchDebounce;
    private readonly SemaphoreSlim _containerListRefreshGate = new(1, 1);

    private const double SparklineWidth = 300;
    private const double SparklineHeight = 72;
    private const int MaxSparkPoints = 90;

    public ContainersViewModel(
        IContainerScreenApi containerApi,
        IDialogService dialogService,
        INotificationService notificationService,
        IAppShutdownToken shutdownToken,
        AppShellActivityState shellActivity,
        IStatsStreamClient statsStream,
        IAppSettingsStore appSettingsStore)
    {
        _containerApi = containerApi;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _shutdownToken = shutdownToken;
        _shellActivity = shellActivity;
        _statsStream = statsStream;
        _appSettingsStore = appSettingsStore;
        _searchDebounce = new SearchDebounceHelper(ApplyFilter);
        _shellActivity.Changed += OnShellActivityChanged;
        RebuildContainerFilterUiLists();
        SelectedFilterKindOption = FilterKindOptions[0];
        SelectedSearchScopeOption = SearchScopeOptions[0];
        UpdateToolbarState();
        RefreshStatsAlertSettingsFromStore();
    }

    /// <summary>
    /// Đọc lại ngưỡng cảnh báo từ file cài đặt (sau Lưu hoặc khi quay lại tab Container).
    /// </summary>
    public void RefreshStatsAlertSettingsFromStore()
    {
        AppSettings s = _appSettingsStore.Load();
        _statsCpuWarnPercent = s.ContainerStatsCpuWarnPercent;
        _statsMemWarnPercent = s.ContainerStatsMemoryWarnPercent;
        UpdateStatsResourceWarnings(_lastCpuForWarn, _lastMemPctForWarn);
        OnPropertyChanged(nameof(SuggestedWslExecCommandText));
        OnPropertyChanged(nameof(SuggestedWslAttachCommandText));
    }

    /// <summary>
    /// Điền lại nhãn ComboBox lọc và phạm vi tìm (gọi khi khởi tạo; có thể mở rộng khi đổi ngôn ngữ UI).
    /// </summary>
    private void RebuildContainerFilterUiLists()
    {
        FilterKindOptions.Clear();
        FilterKindOptions.Add(new FilterKindOption
        {
            Kind = ContainerListFilterKind.All,
            Label = UiLanguageManager.TryLocalizeCurrent("Ui_Containers_Filter_All", "Tất cả"),
        });
        FilterKindOptions.Add(new FilterKindOption
        {
            Kind = ContainerListFilterKind.Running,
            Label = UiLanguageManager.TryLocalizeCurrent("Ui_Containers_Filter_Running", "Đang chạy"),
        });
        FilterKindOptions.Add(new FilterKindOption
        {
            Kind = ContainerListFilterKind.Stopped,
            Label = UiLanguageManager.TryLocalizeCurrent("Ui_Containers_Filter_Stopped", "Đã dừng"),
        });

        SearchScopeOptions.Clear();
        SearchScopeOptions.Add(new SearchScopeOption
        {
            Scope = ContainerSearchScope.All,
            Label = UiLanguageManager.TryLocalizeCurrent("Ui_Containers_SearchScope_All", "Mọi trường"),
        });
        SearchScopeOptions.Add(new SearchScopeOption
        {
            Scope = ContainerSearchScope.Name,
            Label = UiLanguageManager.TryLocalizeCurrent("Ui_Containers_SearchScope_Name", "Tên / ID"),
        });
        SearchScopeOptions.Add(new SearchScopeOption
        {
            Scope = ContainerSearchScope.Image,
            Label = UiLanguageManager.TryLocalizeCurrent("Ui_Containers_SearchScope_Image", "Image"),
        });
        SearchScopeOptions.Add(new SearchScopeOption
        {
            Scope = ContainerSearchScope.Status,
            Label = UiLanguageManager.TryLocalizeCurrent("Ui_Containers_SearchScope_Status", "Trạng thái"),
        });
    }

    /// <summary>Không có container từ Docker (sau khi tải xong).</summary>
    public bool ShowEmptyContainerListHint => !IsBusy && _allItems.Count == 0;

    /// <summary>Có container nhưng lọc/tìm không khớp dòng nào.</summary>
    public bool ShowContainerFilterEmptyHint => !IsBusy && _allItems.Count > 0 && FilteredItems.Count == 0;

    private void ReportNetworkException(Exception ex)
    {
        StatusMessage = NetworkErrorMessageMapper.FormatForUser(ex);
        _ = ApiErrorUiFeedback.ShowNetworkExceptionToastAsync(_notificationService, ex);
    }

    private void ReportNetworkException(Exception ex, Action<string> setLine)
    {
        string msg = NetworkErrorMessageMapper.FormatForUser(ex);
        setLine(msg);
        _ = ApiErrorUiFeedback.ShowNetworkExceptionToastAsync(_notificationService, ex);
    }

    private void NotifyEmptyContainerHints()
    {
        OnPropertyChanged(nameof(ShowEmptyContainerListHint));
        OnPropertyChanged(nameof(ShowContainerFilterEmptyHint));
    }

    /// <summary>
    /// Các mục lọc theo trạng thái (nhãn theo ngôn ngữ tại lúc tạo danh sách).
    /// </summary>
    public ObservableCollection<FilterKindOption> FilterKindOptions { get; } = new();

    /// <summary>
    /// Các mục phạm vi tìm (nhãn theo ngôn ngữ tại lúc tạo danh sách).
    /// </summary>
    public ObservableCollection<SearchScopeOption> SearchScopeOptions { get; } = new();

    [ObservableProperty]
    private FilterKindOption? _selectedFilterKindOption;

    [ObservableProperty]
    private SearchScopeOption? _selectedSearchScopeOption;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private SelectableContainerRow? _selectedContainerRow;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canStart;

    [ObservableProperty]
    private bool _canStop;

    [ObservableProperty]
    private bool _canRestart;

    [ObservableProperty]
    private bool _canRemove;

    [ObservableProperty]
    private bool _canLoadDetail;

    /// <summary>Còn dòng chưa tick: cho phép Chọn tất cả (trong danh sách đã lọc).</summary>
    [ObservableProperty]
    private bool _canSelectAllFiltered;

    /// <summary>Có ít nhất một ô đã chọn: Bỏ chọn, Xóa đã chọn, Sao chép ID.</summary>
    [ObservableProperty]
    private bool _canClearSelection;

    /// <summary>Có ít nhất một dòng đã tick: sao chép ID đầy đủ vào clipboard.</summary>
    [ObservableProperty]
    private bool _canCopySelectedIds;

    /// <summary>Có ít nhất một dòng đã chọn đang dừng: Start đã chọn.</summary>
    [ObservableProperty]
    private bool _canBatchStart;

    /// <summary>Có ít nhất một dòng đã chọn đang chạy: Stop đã chọn.</summary>
    [ObservableProperty]
    private bool _canBatchStop;

    /// <summary>Có ít nhất một dòng đã chọn: Xóa đã chọn.</summary>
    [ObservableProperty]
    private bool _canBatchRemove;

    [ObservableProperty]
    private bool _isDetailLoading;

    [ObservableProperty]
    private string _detailInspectJson = string.Empty;

    /// <summary>
    /// Các trường chính từ inspect (đọc từ JSON Engine), hiển thị trước khối JSON thô.
    /// </summary>
    [ObservableProperty]
    private string _detailInspectSummaryText = string.Empty;

    /// <summary>
    /// Lọc theo chuỗi (không phân biệt hoa thường) trên cột tên hoặc giá trị nhãn.
    /// </summary>
    [ObservableProperty]
    private string _inspectLabelFilterText = string.Empty;

    [ObservableProperty]
    private string _detailStatsText = string.Empty;

    /// <summary>
    /// Dòng text top container theo RAM (API snapshot).
    /// </summary>
    [ObservableProperty]
    private string _topMemorySummaryText = string.Empty;

    /// <summary>
    /// Top container theo CPU % (snapshot).
    /// </summary>
    [ObservableProperty]
    private string _topCpuSummaryText = string.Empty;

    /// <summary>
    /// Khi bật, gọi lại API stats theo chu kỳ (realtime qua polling).
    /// </summary>
    [ObservableProperty]
    private bool _isStatsRealtimeEnabled;

    /// <summary>
    /// Chu kỳ làm mới stats (giây), tối thiểu 1, tối đa 10.
    /// </summary>
    [ObservableProperty]
    private int _statsRealtimeIntervalSeconds = 1;

    /// <summary>
    /// Số lần gọi API stats thành công kể từ khi chọn container hiện tại (realtime).
    /// </summary>
    [ObservableProperty]
    private int _statsRealtimePollCount;

    /// <summary>
    /// Khi bật cùng realtime: dùng WebSocket thay cho polling REST.
    /// </summary>
    [ObservableProperty]
    private bool _useStatsWebSocket;

    /// <summary>
    /// Khoảng gửi tối thiểu giữa hai mẫu trên server (ms), query WebSocket.
    /// </summary>
    [ObservableProperty]
    private int _statsWebSocketIntervalMs = 1000;

    /// <summary>
    /// Cảnh báo khi CPU hoặc RAM vượt ngưỡng (Cài đặt — Hiển thị).
    /// </summary>
    [ObservableProperty]
    private string _statsResourceWarningText = string.Empty;

    [ObservableProperty]
    private bool _statsResourceWarningVisible;

    /// <summary>
    /// Điểm vẽ sparkline CPU % và RAM % (0–100).
    /// </summary>
    public PointCollection CpuSparklinePoints { get; } = new();

    public PointCollection MemorySparklinePoints { get; } = new();

    /// <summary>
    /// Gợi ý lệnh <c>docker exec</c> cho terminal ngoài (Docker CLI trên máy hoặc trong WSL).
    /// </summary>
    public string SuggestedExecCommandText
    {
        get
        {
            ContainerSummaryDto? m = SelectedContainerRow?.Model;
            if (m is null)
            {
                return string.Empty;
            }

            return $"docker exec -it {DockerCliTargetRef(m)} sh";
        }
    }

    /// <summary>
    /// Gợi ý lệnh <c>docker attach</c> cho terminal ngoài.
    /// </summary>
    public string SuggestedAttachCommandText
    {
        get
        {
            ContainerSummaryDto? m = SelectedContainerRow?.Model;
            if (m is null)
            {
                return string.Empty;
            }

            return $"docker attach {DockerCliTargetRef(m)}";
        }
    }

    /// <summary>
    /// Một dòng <c>wsl.exe … bash -c 'docker exec …'</c> để chạy Docker trong distro WSL (theo Cài đặt — Distro WSL).
    /// </summary>
    public string SuggestedWslExecCommandText
    {
        get
        {
            ContainerSummaryDto? m = SelectedContainerRow?.Model;
            if (m is null)
            {
                return string.Empty;
            }

            return BuildWslBashDockerLine($"exec -it {DockerCliTargetRef(m)} sh");
        }
    }

    /// <summary>
    /// Một dòng <c>wsl.exe … bash -c 'docker attach …'</c> (TTY theo container đang chạy).
    /// </summary>
    public string SuggestedWslAttachCommandText
    {
        get
        {
            ContainerSummaryDto? m = SelectedContainerRow?.Model;
            if (m is null)
            {
                return string.Empty;
            }

            return BuildWslBashDockerLine($"attach {DockerCliTargetRef(m)}");
        }
    }

    /// <summary>
    /// Hiện gợi ý khi container đang dừng — exec/attach thường cần container đang chạy.
    /// </summary>
    public bool AttachTtyStoppedHintVisible =>
        SelectedContainerRow?.Model is ContainerSummaryDto x && !IsRunning(x.Status);

    private static string DockerCliTargetRef(ContainerSummaryDto m)
    {
        string id = (m.Id ?? string.Empty).Trim();
        if (id.Length > 0)
        {
            return id;
        }

        string name = (m.Name ?? string.Empty).Trim();
        if (name.Length > 0)
        {
            return name;
        }

        return (m.ShortId ?? string.Empty).Trim();
    }

    private string BuildWslBashDockerLine(string dockerArgumentsWithoutLeadingDocker)
    {
        string inner = "docker " + dockerArgumentsWithoutLeadingDocker;
        string bashC = ComposeComposePaths.BashSingleQuote(inner);
        AppSettings cfg = _appSettingsStore.Load();
        string? distro = string.IsNullOrWhiteSpace(cfg.WslDistribution) ? null : cfg.WslDistribution.Trim();
        if (string.IsNullOrWhiteSpace(distro))
        {
            return $"wsl -- bash -c {bashC}";
        }

        return $"wsl -d {ComposeComposePaths.BashSingleQuote(distro)} -- bash -c {bashC}";
    }

    /// <summary>
    /// Cho phép chỉnh chu kỳ polling REST khi không dùng WebSocket.
    /// </summary>
    public bool CanEditPollingInterval => IsStatsRealtimeEnabled && !UseStatsWebSocket;

    /// <summary>
    /// Cho phép chỉnh interval WebSocket khi đang dùng stream.
    /// </summary>
    public bool CanConfigureWebSocketInterval => IsStatsRealtimeEnabled && UseStatsWebSocket;

    public ObservableCollection<SelectableContainerRow> FilteredItems { get; } = new();

    /// <summary>
    /// Bảng chi tiết từ inspect: mount, cổng, env, nhãn, mạng.
    /// </summary>
    public ObservableCollection<InspectMountRow> DetailInspectMounts { get; } = new();

    public ObservableCollection<InspectPortRow> DetailInspectPorts { get; } = new();

    public ObservableCollection<InspectEnvRow> DetailInspectEnv { get; } = new();

    public ObservableCollection<InspectLabelRow> DetailInspectLabels { get; } = new();

    public ObservableCollection<InspectNetworkRow> DetailInspectNetworks { get; } = new();

    private readonly List<InspectLabelRow> _allInspectLabelRows = new();

    /// <summary>
    /// Lựa chọn khoảng thời gian làm mới stats (giây).
    /// </summary>
    public IReadOnlyList<int> StatsRealtimeIntervalChoices { get; } = new[] { 1, 2, 3, 5, 10 };

    /// <summary>
    /// Lựa chọn interval tối thiểu giữa hai mẫu WebSocket (ms).
    /// </summary>
    public IReadOnlyList<int> StatsWebSocketIntervalChoices { get; } = new[] { 500, 1000, 2000, 3000, 5000 };

    partial void OnSelectedContainerRowChanged(SelectableContainerRow? value)
    {
        ResetInspectDetailPanels();
        StatsRealtimePollCount = 0;
        if (value is null)
        {
            IsStatsRealtimeEnabled = false;
        }

        OnPropertyChanged(nameof(SuggestedExecCommandText));
        OnPropertyChanged(nameof(SuggestedAttachCommandText));
        OnPropertyChanged(nameof(SuggestedWslExecCommandText));
        OnPropertyChanged(nameof(SuggestedWslAttachCommandText));
        OnPropertyChanged(nameof(AttachTtyStoppedHintVisible));
        RestartStatsRealtimeDelivery();
        UpdateToolbarState();
    }

    partial void OnUseStatsWebSocketChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditPollingInterval));
        OnPropertyChanged(nameof(CanConfigureWebSocketInterval));
        RestartStatsRealtimeDelivery();
    }

    partial void OnStatsWebSocketIntervalMsChanged(int value)
    {
        if (IsStatsRealtimeEnabled && UseStatsWebSocket)
        {
            RestartStatsRealtimeDelivery();
        }
    }

    partial void OnInspectLabelFilterTextChanged(string value)
    {
        ApplyInspectLabelFilter();
    }

    private void ResetInspectDetailPanels()
    {
        DetailInspectJson = string.Empty;
        DetailInspectSummaryText = string.Empty;
        DetailStatsText = string.Empty;
        ClearInspectGridsOnly();
        ClearSparklines();
    }

    private void ClearInspectGridsOnly()
    {
        DetailInspectMounts.Clear();
        DetailInspectPorts.Clear();
        DetailInspectEnv.Clear();
        DetailInspectLabels.Clear();
        DetailInspectNetworks.Clear();
        _allInspectLabelRows.Clear();
        InspectLabelFilterText = string.Empty;
    }

    private void ApplyInspectLabelFilter()
    {
        DetailInspectLabels.Clear();
        string f = (InspectLabelFilterText ?? string.Empty).Trim();
        foreach (InspectLabelRow row in _allInspectLabelRows)
        {
            if (string.IsNullOrEmpty(f)
                || row.Key.Contains(f, StringComparison.OrdinalIgnoreCase)
                || row.Value.Contains(f, StringComparison.OrdinalIgnoreCase))
            {
                DetailInspectLabels.Add(row);
            }
        }
    }

    partial void OnSelectedFilterKindOptionChanged(FilterKindOption? value)
    {
        ApplyFilter();
    }

    partial void OnSelectedSearchScopeOptionChanged(SearchScopeOption? value)
    {
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounce.Schedule();
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyEmptyContainerHints();
        UpdateToolbarState();
    }

    partial void OnIsDetailLoadingChanged(bool value)
    {
        UpdateToolbarState();
    }

    partial void OnIsStatsRealtimeEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditPollingInterval));
        OnPropertyChanged(nameof(CanConfigureWebSocketInterval));
        if (value)
        {
            RestartStatsRealtimeDelivery();
        }
        else
        {
            StopStatsWebSocket();
            StopStatsRealtimeTimer();
            _ = RefreshStatsTextWithoutRealtimeHintAsync();
        }

        UpdateToolbarState();
    }

    partial void OnStatsRealtimeIntervalSecondsChanged(int value)
    {
        RestartStatsRealtimeDelivery();
    }

    /// <summary>
    /// Tải lại từ API và áp dụng lọc/tìm.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!_shellActivity.ShouldRefreshContainerList)
        {
            return;
        }

        if (!await _containerListRefreshGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            try
            {
                ApiResult<ContainerListData> list = await _containerApi.GetContainersAsync(_shutdownToken.Token).ConfigureAwait(true);
                if (!list.Success)
                {
                    string err = list.Error?.Message
                        ?? UiLanguageManager.TryLocalizeCurrent("Ui_Status_Common_ListLoadFailed", "Không đọc được danh sách.");
                    StatusMessage = err;
                    _ = ApiErrorUiFeedback.ShowWarningToastAsync(_notificationService, err);
                    _allItems = new List<ContainerSummaryDto>();
                    ApplyFilter();
                    return;
                }

                _allItems = list.Data?.Items ?? new List<ContainerSummaryDto>();
                ApplyFilter();
                RefreshStatsAlertSettingsFromStore();
                StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                    "Ui_Containers_Status_LoadedContainersCountFormat",
                    "Đã tải {0} container.",
                    _allItems.Count);
            }
            catch (Exception ex)
            {
                ReportNetworkException(ex);
                _allItems = new List<ContainerSummaryDto>();
                ApplyFilter();
            }
            finally
            {
                IsBusy = false;
                UpdateToolbarState();
            }
        }
        finally
        {
            _containerListRefreshGate.Release();
        }
    }

    /// <summary>
    /// Sao chép lệnh gợi ý docker exec vào clipboard.
    /// </summary>
    [RelayCommand]
    private void CopySuggestedExecCommand()
    {
        string t = SuggestedExecCommandText;
        if (string.IsNullOrEmpty(t))
        {
            return;
        }

        Clipboard.SetText(t);
        StatusMessage = UiLanguageManager.TryLocalizeCurrent(
            "Ui_Containers_Status_CopyExecDone",
            "Đã sao chép lệnh docker exec.");
    }

    /// <summary>
    /// Sao chép lệnh gợi ý docker attach vào clipboard.
    /// </summary>
    [RelayCommand]
    private void CopySuggestedAttachCommand()
    {
        string t = SuggestedAttachCommandText;
        if (string.IsNullOrEmpty(t))
        {
            return;
        }

        Clipboard.SetText(t);
        StatusMessage = UiLanguageManager.TryLocalizeCurrent(
            "Ui_Containers_Status_CopyAttachDone",
            "Đã sao chép lệnh docker attach.");
    }

    /// <summary>
    /// Sao chép lệnh gợi ý wsl + docker exec vào clipboard.
    /// </summary>
    [RelayCommand]
    private void CopySuggestedWslExecCommand()
    {
        string t = SuggestedWslExecCommandText;
        if (string.IsNullOrEmpty(t))
        {
            return;
        }

        Clipboard.SetText(t);
        StatusMessage = UiLanguageManager.TryLocalizeCurrent(
            "Ui_Containers_Status_CopyWslExecDone",
            "Đã sao chép lệnh WSL (docker exec).");
    }

    /// <summary>
    /// Sao chép lệnh gợi ý wsl + docker attach vào clipboard.
    /// </summary>
    [RelayCommand]
    private void CopySuggestedWslAttachCommand()
    {
        string t = SuggestedWslAttachCommandText;
        if (string.IsNullOrEmpty(t))
        {
            return;
        }

        Clipboard.SetText(t);
        StatusMessage = UiLanguageManager.TryLocalizeCurrent(
            "Ui_Containers_Status_CopyWslAttachDone",
            "Đã sao chép lệnh WSL (docker attach).");
    }

    /// <summary>
    /// Top 5 container đang chạy theo dùng RAM (một lần chụp stats trên server).
    /// </summary>
    [RelayCommand]
    private async Task RefreshTopMemoryAsync()
    {
        IsBusy = true;
        TopMemorySummaryText = string.Empty;
        try
        {
            ApiResult<ContainerTopMemoryData> res = await _containerApi.GetContainersTopByMemoryAsync(5, _shutdownToken.Token).ConfigureAwait(true);
            if (!res.Success)
            {
                TopMemorySummaryText = res.Error?.Message ?? "Không đọc được bảng top RAM.";
                return;
            }

            List<ContainerTopMemoryRowDto> items = res.Data?.Items ?? new List<ContainerTopMemoryRowDto>();
            if (items.Count == 0)
            {
                TopMemorySummaryText = "Không có container đang chạy hoặc không có dữ liệu stats.";
                return;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                ContainerTopMemoryRowDto x = items[i];
                double mib = x.MemoryUsageBytes / (1024.0 * 1024.0);
                sb.Append(i + 1)
                    .Append(". ")
                    .Append(string.IsNullOrEmpty(x.Name) ? x.ShortId : x.Name)
                    .Append(" (")
                    .Append(x.ShortId)
                    .Append(") — RAM ~")
                    .Append(mib.ToString("F1", CultureInfo.InvariantCulture))
                    .Append(" MiB, CPU ")
                    .Append(x.CpuUsagePercent.ToString("F1", CultureInfo.InvariantCulture))
                    .Append("% — ")
                    .Append(x.Image)
                    .AppendLine();
            }

            TopMemorySummaryText = sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex, m => TopMemorySummaryText = m);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Top 5 container đang chạy theo CPU % (một lần chụp stats trên server).
    /// </summary>
    [RelayCommand]
    private async Task RefreshTopCpuAsync()
    {
        IsBusy = true;
        TopCpuSummaryText = string.Empty;
        try
        {
            ApiResult<ContainerTopMemoryData> res = await _containerApi.GetContainersTopByCpuAsync(5, _shutdownToken.Token).ConfigureAwait(true);
            if (!res.Success)
            {
                TopCpuSummaryText = res.Error?.Message ?? "Không đọc được bảng top CPU.";
                return;
            }

            List<ContainerTopMemoryRowDto> items = res.Data?.Items ?? new List<ContainerTopMemoryRowDto>();
            if (items.Count == 0)
            {
                TopCpuSummaryText = "Không có container đang chạy hoặc không có dữ liệu stats.";
                return;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                ContainerTopMemoryRowDto x = items[i];
                double mib = x.MemoryUsageBytes / (1024.0 * 1024.0);
                sb.Append(i + 1)
                    .Append(". ")
                    .Append(string.IsNullOrEmpty(x.Name) ? x.ShortId : x.Name)
                    .Append(" (")
                    .Append(x.ShortId)
                    .Append(") — CPU ")
                    .Append(x.CpuUsagePercent.ToString("F1", CultureInfo.InvariantCulture))
                    .Append(" %, RAM ~")
                    .Append(mib.ToString("F1", CultureInfo.InvariantCulture))
                    .Append(" MiB — ")
                    .Append(x.Image)
                    .AppendLine();
            }

            TopCpuSummaryText = sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex, m => TopCpuSummaryText = m);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StartSelectedAsync()
    {
        if (SelectedContainerRow is null)
        {
            return;
        }

        await RunActionAsync(() => _containerApi.StartContainerAsync(SelectedContainerRow.Model.Id, _shutdownToken.Token));
    }

    [RelayCommand]
    private async Task StopSelectedAsync()
    {
        if (SelectedContainerRow is null)
        {
            return;
        }

        await RunActionAsync(() => _containerApi.StopContainerAsync(SelectedContainerRow.Model.Id, _shutdownToken.Token));
    }

    [RelayCommand]
    private async Task RestartSelectedAsync()
    {
        if (SelectedContainerRow is null)
        {
            return;
        }

        await RunActionAsync(() => _containerApi.RestartContainerAsync(SelectedContainerRow.Model.Id, _shutdownToken.Token));
    }

    /// <summary>
    /// Tải inspect + một snapshot stats cho container đang chọn (mở rộng mục 1–2 kế hoạch).
    /// </summary>
    [RelayCommand]
    private async Task LoadDetailAsync()
    {
        if (SelectedContainerRow is null)
        {
            return;
        }

        IsDetailLoading = true;
        ResetInspectDetailPanels();
        StatusMessage = string.Empty;
        try
        {
            string id = SelectedContainerRow.Model.Id;
            Task<ApiResult<ContainerInspectData>> tInspect = _containerApi.GetContainerInspectAsync(id, _shutdownToken.Token);
            Task<ApiResult<ContainerStatsSnapshotData>> tStats = _containerApi.GetContainerStatsAsync(id, _shutdownToken.Token);
            await Task.WhenAll(tInspect, tStats).ConfigureAwait(true);
            ApiResult<ContainerInspectData> rInsp = await tInspect.ConfigureAwait(true);
            ApiResult<ContainerStatsSnapshotData> rStats = await tStats.ConfigureAwait(true);
            if (!rInsp.Success)
            {
                StatusMessage = rInsp.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Containers_Status_InspectFailed", "Không đọc được inspect.");
                return;
            }

            if (!rStats.Success)
            {
                StatusMessage = rStats.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Containers_Status_StatsFailed", "Không đọc được stats.");
                return;
            }

            JsonElement inspect = rInsp.Data!.Inspect;
            DetailInspectSummaryText = ContainerInspectSummaryFormatter.Format(inspect);
            ContainerInspectGridParser.Fill(
                inspect,
                DetailInspectMounts,
                DetailInspectPorts,
                DetailInspectEnv,
                _allInspectLabelRows,
                DetailInspectNetworks);
            ApplyInspectLabelFilter();
            DetailInspectJson = JsonSerializer.Serialize(
                inspect,
                new JsonSerializerOptions { WriteIndented = true });
            DetailStatsText = FormatStatsSnapshot(rStats.Data!, IsStatsRealtimeEnabled);
            PushSparkSample(rStats.Data!, incrementPollCount: false);
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Containers_Status_DetailLoaded",
                "Đã tải chi tiết (inspect + stats).");
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex);
        }
        finally
        {
            IsDetailLoading = false;
            UpdateToolbarState();
        }
    }

    [RelayCommand]
    private void SelectAllFiltered()
    {
        foreach (SelectableContainerRow row in FilteredItems)
        {
            row.IsSelected = true;
        }

        UpdateBatchToolbarState();
    }

    [RelayCommand]
    private void ClearRowSelectionChecks()
    {
        foreach (SelectableContainerRow row in FilteredItems)
        {
            row.IsSelected = false;
        }

        UpdateBatchToolbarState();
    }

    /// <summary>
    /// Sao chép ID đầy đủ của các dòng đã tick (một ID mỗi dòng).
    /// </summary>
    [RelayCommand]
    private void CopySelectedIdsToClipboard()
    {
        List<string> ids = FilteredItems.Where(r => r.IsSelected).Select(r => r.Model.Id).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        Clipboard.SetText(string.Join(Environment.NewLine, ids));
        StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
            "Ui_Containers_Status_CopyIdsDoneFormat",
            "Đã sao chép {0} ID container vào clipboard.",
            ids.Count);
    }

    [RelayCommand]
    private async Task BatchStartAsync()
    {
        List<SelectableContainerRow> targets = FilteredItems
            .Where(r => r.IsSelected && !IsRunning(r.Model.Status))
            .ToList();
        if (targets.Count == 0)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Containers_Status_NoRowsToStart",
                "Không có dòng đã chọn cần start (hoặc đang chạy).");
            return;
        }

        bool ok = await _dialogService
            .ConfirmAsync(
                $"Start {targets.Count} container đã chọn?",
                "DockLite",
                DialogConfirmKind.Question)
            .ConfigureAwait(true);
        if (!ok)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            foreach (SelectableContainerRow t in targets)
            {
                ApiResult<EmptyApiPayload> res = await _containerApi.StartContainerAsync(t.Model.Id, _shutdownToken.Token).ConfigureAwait(true);
                if (!res.Success)
                {
                    StatusMessage = res.Error?.Message
                        ?? UiLanguageManager.TryLocalizeCurrent("Ui_Containers_Status_StartFailed", "Start thất bại.");
                    return;
                }
            }

            await RefreshAsync().ConfigureAwait(true);
            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Containers_Status_StartedCountFormat",
                "Đã start {0} container.",
                targets.Count);
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex);
        }
        finally
        {
            IsBusy = false;
            UpdateToolbarState();
        }
    }

    [RelayCommand]
    private async Task BatchStopAsync()
    {
        List<SelectableContainerRow> targets = FilteredItems
            .Where(r => r.IsSelected && IsRunning(r.Model.Status))
            .ToList();
        if (targets.Count == 0)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Containers_Status_NoRowsToStop",
                "Không có dòng đã chọn đang chạy để stop.");
            return;
        }

        bool ok = await _dialogService
            .ConfirmAsync(
                $"Stop {targets.Count} container đã chọn?",
                "DockLite",
                DialogConfirmKind.Question)
            .ConfigureAwait(true);
        if (!ok)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            foreach (SelectableContainerRow t in targets)
            {
                ApiResult<EmptyApiPayload> res = await _containerApi.StopContainerAsync(t.Model.Id, _shutdownToken.Token).ConfigureAwait(true);
                if (!res.Success)
                {
                    StatusMessage = res.Error?.Message
                        ?? UiLanguageManager.TryLocalizeCurrent("Ui_Containers_Status_StopFailed", "Stop thất bại.");
                    return;
                }
            }

            await RefreshAsync().ConfigureAwait(true);
            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Containers_Status_StoppedCountFormat",
                "Đã stop {0} container.",
                targets.Count);
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex);
        }
        finally
        {
            IsBusy = false;
            UpdateToolbarState();
        }
    }

    [RelayCommand]
    private async Task BatchRemoveCheckedAsync()
    {
        List<SelectableContainerRow> targets = FilteredItems.Where(r => r.IsSelected).ToList();
        if (targets.Count == 0)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Containers_Status_SelectAtLeastOne",
                "Chọn ít nhất một container (ô chọn).");
            return;
        }

        bool ok = await _dialogService
            .ConfirmAsync(
                $"Xóa {targets.Count} container đã chọn?",
                "DockLite",
                DialogConfirmKind.Question)
            .ConfigureAwait(true);
        if (!ok)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            foreach (SelectableContainerRow t in targets)
            {
                bool force = IsRunning(t.Model.Status);
                ApiResult<EmptyApiPayload> res = await _containerApi.RemoveContainerAsync(t.Model.Id, force, _shutdownToken.Token).ConfigureAwait(true);
                if (!res.Success)
                {
                    StatusMessage = res.Error?.Message
                        ?? UiLanguageManager.TryLocalizeCurrent("Ui_Containers_Status_DeleteFailed", "Xóa thất bại.");
                    return;
                }
            }

            await RefreshAsync().ConfigureAwait(true);
            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Containers_Status_DeletedCountFormat",
                "Đã xóa {0} container.",
                targets.Count);
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex);
        }
        finally
        {
            IsBusy = false;
            UpdateToolbarState();
        }
    }

    [RelayCommand]
    private async Task RemoveSelectedAsync()
    {
        if (SelectedContainerRow is null)
        {
            return;
        }

        bool ok = await _dialogService
            .ConfirmAsync(
                $"Xóa container \"{SelectedContainerRow.Model.Name}\" ({SelectedContainerRow.Model.ShortId})?",
                "DockLite",
                DialogConfirmKind.Question)
            .ConfigureAwait(true);
        if (!ok)
        {
            return;
        }

        bool force = IsRunning(SelectedContainerRow.Model.Status);
        await RunActionAsync(() => _containerApi.RemoveContainerAsync(SelectedContainerRow.Model.Id, force, _shutdownToken.Token));
    }

    private async Task RunActionAsync(Func<Task<ApiResult<EmptyApiPayload>>> call)
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            ApiResult<EmptyApiPayload> res = await call().ConfigureAwait(true);
            if (!res.Success)
            {
                StatusMessage = string.IsNullOrWhiteSpace(res.Error?.Message)
                    ? UiLanguageManager.TryLocalizeCurrent("Ui_Status_Common_OperationFailed", "Thao tác thất bại.")
                    : res.Error!.Message;
                return;
            }

            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex);
        }
        finally
        {
            IsBusy = false;
            UpdateToolbarState();
        }
    }

    private void ApplyFilter()
    {
        foreach (SelectableContainerRow row in FilteredItems)
        {
            row.PropertyChanged -= OnFilteredRowPropertyChanged;
        }

        FilteredItems.Clear();
        string q = SearchText.Trim();
        ContainerSearchScope scope = SelectedSearchScopeOption?.Scope ?? ContainerSearchScope.All;
        foreach (ContainerSummaryDto item in _allItems)
        {
            if (!MatchesFilterKind(item))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(q) && !MatchesSearch(item, q, scope))
            {
                continue;
            }

            var row = new SelectableContainerRow(item);
            row.PropertyChanged += OnFilteredRowPropertyChanged;
            FilteredItems.Add(row);
        }

        if (SelectedContainerRow is not null && !FilteredItems.Contains(SelectedContainerRow))
        {
            SelectedContainerRow = null;
        }

        NotifyEmptyContainerHints();
        UpdateToolbarState();
    }

    private bool MatchesFilterKind(ContainerSummaryDto item)
    {
        return SelectedFilterKindOption?.Kind switch
        {
            ContainerListFilterKind.Running => IsRunning(item.Status),
            ContainerListFilterKind.Stopped => IsStopped(item.Status),
            _ => true,
        };
    }

    private static bool MatchesSearch(ContainerSummaryDto item, string q, ContainerSearchScope scope)
    {
        return scope switch
        {
            ContainerSearchScope.Name => MatchesSearchNameOrId(item, q),
            ContainerSearchScope.Image => item.Image.Contains(q, StringComparison.OrdinalIgnoreCase),
            ContainerSearchScope.Status => item.Status.Contains(q, StringComparison.OrdinalIgnoreCase),
            _ => MatchesSearchAllFields(item, q),
        };
    }

    private static bool MatchesSearchNameOrId(ContainerSummaryDto item, string q)
    {
        return item.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
               || item.ShortId.Contains(q, StringComparison.OrdinalIgnoreCase)
               || item.Name.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSearchAllFields(ContainerSummaryDto item, string q)
    {
        if (item.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
            || item.ShortId.Contains(q, StringComparison.OrdinalIgnoreCase)
            || item.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || item.Image.Contains(q, StringComparison.OrdinalIgnoreCase)
            || item.Status.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (item.Ports?.Contains(q, StringComparison.OrdinalIgnoreCase) == true
            || item.Command?.Contains(q, StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        if (item.Labels is not null)
        {
            foreach (KeyValuePair<string, string> kv in item.Labels)
            {
                if (kv.Key.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || kv.Value.Contains(q, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsRunning(string status)
    {
        return status.StartsWith("Up ", StringComparison.OrdinalIgnoreCase)
               || status.Equals("running", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStopped(string status)
    {
        return status.Contains("Exited", StringComparison.OrdinalIgnoreCase)
               || status.Contains("Created", StringComparison.OrdinalIgnoreCase)
               || status.Contains("Dead", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateToolbarState()
    {
        ContainerSummaryDto? s = SelectedContainerRow?.Model;
        if (s is null || IsBusy)
        {
            CanStart = false;
            CanStop = false;
            CanRestart = false;
            CanRemove = false;
            CanLoadDetail = false;
        }
        else
        {
            bool running = IsRunning(s.Status);
            CanStart = !running;
            CanStop = running;
            CanRestart = true;
            CanRemove = true;
            CanLoadDetail = !IsDetailLoading;
        }

        UpdateBatchToolbarState();
    }

    private void OnFilteredRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableContainerRow.IsSelected))
        {
            UpdateBatchToolbarState();
        }
    }

    /// <summary>
    /// Nút Chọn tất cả / Bỏ chọn / Start|Stop|Xóa đã chọn — phụ thuộc ô tick và trạng thái từng dòng.
    /// </summary>
    private void UpdateBatchToolbarState()
    {
        if (IsBusy)
        {
            CanSelectAllFiltered = false;
            CanClearSelection = false;
            CanCopySelectedIds = false;
            CanBatchStart = false;
            CanBatchStop = false;
            CanBatchRemove = false;
            CanBatchStats = false;
            return;
        }

        int n = FilteredItems.Count;
        int selectedCount = FilteredItems.Count(r => r.IsSelected);
        bool allSelected = n > 0 && selectedCount == n;
        bool anySelected = selectedCount > 0;

        CanSelectAllFiltered = n > 0 && !allSelected;
        CanClearSelection = anySelected;
        CanCopySelectedIds = anySelected;
        CanBatchStart = FilteredItems.Any(r => r.IsSelected && !IsRunning(r.Model.Status));
        CanBatchStop = FilteredItems.Any(r => r.IsSelected && IsRunning(r.Model.Status));
        CanBatchRemove = anySelected;
        CanBatchStats = anySelected && selectedCount >= 2 && selectedCount <= 32;
    }

    private void OnShellActivityChanged(object? sender, EventArgs e)
    {
        RestartStatsRealtimeDelivery();
    }

    /// <summary>
    /// Bật lại polling REST hoặc WebSocket stats theo cài đặt và trạng thái tab/cửa sổ.
    /// </summary>
    private void RestartStatsRealtimeDelivery()
    {
        StopStatsWebSocket();
        StopStatsRealtimeTimer();
        if (!IsStatsRealtimeEnabled || SelectedContainerRow is null)
        {
            return;
        }

        if (!_shellActivity.ShouldPollContainerStats)
        {
            return;
        }

        if (UseStatsWebSocket)
        {
            StartStatsWebSocketLoop();
            return;
        }

        _statsRealtimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Clamp(StatsRealtimeIntervalSeconds, 1, 10)),
        };
        _statsRealtimeTimer.Tick += StatsRealtimeTimerOnTick;
        _statsRealtimeTimer.Start();
        _ = FetchStatsSnapshotOnceAsync();
    }

    private void StopStatsWebSocket()
    {
        try
        {
            _statsWsCts?.Cancel();
        }
        catch
        {
            // Bỏ qua.
        }

        _statsWsCts?.Dispose();
        _statsWsCts = null;
    }

    private void StartStatsWebSocketLoop()
    {
        if (SelectedContainerRow is null)
        {
            return;
        }

        StopStatsWebSocket();
        _statsWsCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownToken.Token);
        CancellationToken ct = _statsWsCts.Token;
        string id = SelectedContainerRow.Model.Id;
        int interval = Math.Clamp(StatsWebSocketIntervalMs, 500, 5000);
        _ = StreamStatsLoopAsync(id, interval, ct);
    }

    private async Task StreamStatsLoopAsync(string containerId, int intervalMs, CancellationToken ct)
    {
        try
        {
            await _statsStream
                .StreamStatsAsync(
                    containerId,
                    intervalMs,
                    d =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (SelectedContainerRow is null
                                || !string.Equals(SelectedContainerRow.Model.Id, containerId, StringComparison.Ordinal))
                            {
                                return;
                            }

                            ApplyRealtimeStatsSample(d);
                        });
                    },
                    ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Đóng tab hoặc tắt realtime.
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ReportNetworkException(ex);
            });
        }
    }

    private void ApplyRealtimeStatsSample(ContainerStatsSnapshotData d)
    {
        DetailStatsText = FormatStatsSnapshot(d, true);
        PushSparkSample(d, incrementPollCount: true);
    }

    private void PushSparkSample(ContainerStatsSnapshotData d, bool incrementPollCount)
    {
        if (incrementPollCount)
        {
            StatsRealtimePollCount++;
        }

        double cpu = Math.Clamp(d.CpuUsagePercent, 0, 100);
        double memPct = 0;
        if (d.MemoryLimitBytes > 0)
        {
            memPct = 100.0 * d.MemoryUsageBytes / d.MemoryLimitBytes;
        }

        memPct = Math.Clamp(memPct, 0, 100);
        _cpuSparkHistory.Add(cpu);
        if (_cpuSparkHistory.Count > MaxSparkPoints)
        {
            _cpuSparkHistory.RemoveAt(0);
        }

        _memorySparkHistory.Add(memPct);
        if (_memorySparkHistory.Count > MaxSparkPoints)
        {
            _memorySparkHistory.RemoveAt(0);
        }

        _lastCpuForWarn = cpu;
        _lastMemPctForWarn = memPct;
        RebuildSparklines();
        UpdateStatsResourceWarnings(cpu, memPct);
    }

    private void UpdateStatsResourceWarnings(double cpuPercent, double memoryPercentOfLimit)
    {
        if (_statsCpuWarnPercent <= 0 && _statsMemWarnPercent <= 0)
        {
            StatsResourceWarningVisible = false;
            StatsResourceWarningText = string.Empty;
            return;
        }

        bool cpuHit = _statsCpuWarnPercent > 0 && cpuPercent >= _statsCpuWarnPercent;
        bool memHit = _statsMemWarnPercent > 0 && memoryPercentOfLimit >= _statsMemWarnPercent;
        if (!cpuHit && !memHit)
        {
            StatsResourceWarningVisible = false;
            StatsResourceWarningText = string.Empty;
            return;
        }

        StatsResourceWarningVisible = true;
        var parts = new List<string>(2);
        if (cpuHit)
        {
            parts.Add(
                UiLanguageManager.TryLocalizeFormatCurrent(
                    "Ui_Containers_StatsWarnCpuPart",
                    "CPU {0:F1}% (ngưỡng {1}%)",
                    cpuPercent,
                    _statsCpuWarnPercent));
        }

        if (memHit)
        {
            parts.Add(
                UiLanguageManager.TryLocalizeFormatCurrent(
                    "Ui_Containers_StatsWarnMemPart",
                    "RAM {0:F1}% dùng/giới hạn (ngưỡng {1}%)",
                    memoryPercentOfLimit,
                    _statsMemWarnPercent));
        }

        StatsResourceWarningText = string.Join(" · ", parts);
    }

    private void ClearSparklines()
    {
        _cpuSparkHistory.Clear();
        _memorySparkHistory.Clear();
        CpuSparklinePoints.Clear();
        MemorySparklinePoints.Clear();
        _lastCpuForWarn = 0;
        _lastMemPctForWarn = 0;
        StatsResourceWarningVisible = false;
        StatsResourceWarningText = string.Empty;
    }

    private void RebuildSparklines()
    {
        RebuildSparkline(CpuSparklinePoints, _cpuSparkHistory);
        RebuildSparkline(MemorySparklinePoints, _memorySparkHistory);
    }

    private static void RebuildSparkline(PointCollection pts, List<double> values)
    {
        pts.Clear();
        int n = values.Count;
        if (n == 0)
        {
            return;
        }

        double w = SparklineWidth;
        double h = SparklineHeight;
        const double pad = 4;
        for (int i = 0; i < n; i++)
        {
            double x = n == 1 ? w / 2 : pad + ((w - 2 * pad) * i / (n - 1));
            double v = values[i] / 100.0;
            double y = pad + ((h - 2 * pad) * (1 - v));
            pts.Add(new Point(x, y));
        }
    }

    private void StopStatsRealtimeTimer()
    {
        if (_statsRealtimeTimer is null)
        {
            return;
        }

        _statsRealtimeTimer.Tick -= StatsRealtimeTimerOnTick;
        _statsRealtimeTimer.Stop();
        _statsRealtimeTimer = null;
    }

    private async void StatsRealtimeTimerOnTick(object? sender, EventArgs e)
    {
        if (_shutdownToken.Token.IsCancellationRequested)
        {
            return;
        }

        if (!_shellActivity.ShouldPollContainerStats)
        {
            return;
        }

        await FetchStatsSnapshotOnceAsync().ConfigureAwait(true);
    }

    private async Task FetchStatsSnapshotOnceAsync()
    {
        if (SelectedContainerRow is null || !IsStatsRealtimeEnabled || UseStatsWebSocket)
        {
            return;
        }

        if (!await _statsRealtimeGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            ApiResult<ContainerStatsSnapshotData> r = await _containerApi
                .GetContainerStatsAsync(SelectedContainerRow.Model.Id, _shutdownToken.Token)
                .ConfigureAwait(true);
            if (r.Success && r.Data is not null)
            {
                ApplyRealtimeStatsSample(r.Data);
            }
        }
        catch
        {
            // Giữ giá trị stats trước đó; không làm đứng timer.
        }
        finally
        {
            _statsRealtimeGate.Release();
        }
    }

    private async Task RefreshStatsTextWithoutRealtimeHintAsync()
    {
        if (SelectedContainerRow is null)
        {
            return;
        }

        if (!await _statsRealtimeGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            ApiResult<ContainerStatsSnapshotData> r = await _containerApi
                .GetContainerStatsAsync(SelectedContainerRow.Model.Id, _shutdownToken.Token)
                .ConfigureAwait(true);
            if (r.Success && r.Data is not null)
            {
                DetailStatsText = FormatStatsSnapshot(r.Data, false);
            }
        }
        catch
        {
            // Bỏ qua khi tắt realtime.
        }
        finally
        {
            _statsRealtimeGate.Release();
        }
    }

    private string FormatStatsSnapshot(ContainerStatsSnapshotData d, bool includeRealtimeHint)
    {
        var lines = new List<string>
        {
            $"Thời điểm: {d.ReadAt}",
            $"CPU: {d.CpuUsagePercent:F1} %",
            $"RAM: {FormatBytes(d.MemoryUsageBytes)} / {FormatBytes(d.MemoryLimitBytes)}",
            $"Mạng RX: {FormatBytes(d.NetworkRxBytes)}  TX: {FormatBytes(d.NetworkTxBytes)}",
            $"Ổ (blkio) đọc: {FormatBytes(d.BlockReadBytes)}  ghi: {FormatBytes(d.BlockWriteBytes)}",
        };
        if (includeRealtimeHint)
        {
            lines.Add(string.Empty);
            lines.Add(
                UseStatsWebSocket
                    ? "(Realtime: WebSocket stream)"
                    : "(Realtime: làm mới qua API định kỳ)");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatBytes(ulong bytes)
    {
        const ulong k = 1024;
        if (bytes < k)
        {
            return $"{bytes} B";
        }

        double v = bytes;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        while (v >= k && i < units.Length - 1)
        {
            v /= k;
            i++;
        }

        return $"{v:0.##} {units[i]}";
    }
}
