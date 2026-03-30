using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Models;
using DockLite.App.Services;
using DockLite.Contracts.Api;
using DockLite.Core;
using DockLite.Core.Services;

namespace DockLite.App.ViewModels;

/// <summary>
/// Danh sách container: làm mới, lọc, tìm, thao tác start/stop/restart/xóa.
/// </summary>
public partial class ContainersViewModel : ObservableObject
{
    private readonly IDockLiteApiClient _apiClient;
    private readonly IDialogService _dialogService;
    private readonly IAppShutdownToken _shutdownToken;
    private List<ContainerSummaryDto> _allItems = new();
    private DispatcherTimer? _statsRealtimeTimer;
    private readonly SemaphoreSlim _statsRealtimeGate = new(1, 1);

    public ContainersViewModel(IDockLiteApiClient apiClient, IDialogService dialogService, IAppShutdownToken shutdownToken)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;
        _shutdownToken = shutdownToken;
        UpdateToolbarState();
    }

    [ObservableProperty]
    private string _filterKind = "Tất cả";

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

    /// <summary>Có ít nhất một ô đã chọn: Bỏ chọn, Xóa đã chọn.</summary>
    [ObservableProperty]
    private bool _canClearSelection;

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

    public ObservableCollection<SelectableContainerRow> FilteredItems { get; } = new();

    /// <summary>
    /// Lựa chọn khoảng thời gian làm mới stats (giây).
    /// </summary>
    public IReadOnlyList<int> StatsRealtimeIntervalChoices { get; } = new[] { 1, 2, 3, 5, 10 };

    /// <summary>
    /// Giá trị cho ComboBox lọc (khớp <see cref="FilterKind"/>).
    /// </summary>
    public IReadOnlyList<string> FilterKinds { get; } = new[] { "Tất cả", "Đang chạy", "Đã dừng" };

    partial void OnSelectedContainerRowChanged(SelectableContainerRow? value)
    {
        DetailInspectJson = string.Empty;
        DetailStatsText = string.Empty;
        if (value is null)
        {
            IsStatsRealtimeEnabled = false;
        }

        RestartStatsRealtimeTimerIfNeeded();
        UpdateToolbarState();
    }

    partial void OnFilterKindChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnIsBusyChanged(bool value)
    {
        UpdateToolbarState();
    }

    partial void OnIsDetailLoadingChanged(bool value)
    {
        UpdateToolbarState();
    }

    partial void OnIsStatsRealtimeEnabledChanged(bool value)
    {
        if (value)
        {
            RestartStatsRealtimeTimerIfNeeded();
        }
        else
        {
            StopStatsRealtimeTimer();
            _ = RefreshStatsTextWithoutRealtimeHintAsync();
        }

        UpdateToolbarState();
    }

    partial void OnStatsRealtimeIntervalSecondsChanged(int value)
    {
        RestartStatsRealtimeTimerIfNeeded();
    }

    /// <summary>
    /// Tải lại từ API và áp dụng lọc/tìm.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            ApiResult<ContainerListData> list = await _apiClient.GetContainersAsync(_shutdownToken.Token).ConfigureAwait(true);
            if (!list.Success)
            {
                StatusMessage = list.Error?.Message ?? "Không đọc được danh sách.";
                _allItems = new List<ContainerSummaryDto>();
                ApplyFilter();
                return;
            }

            _allItems = list.Data?.Items ?? new List<ContainerSummaryDto>();
            ApplyFilter();
            StatusMessage = $"Đã tải {_allItems.Count} container.";
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
            _allItems = new List<ContainerSummaryDto>();
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
            UpdateToolbarState();
        }
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
            ApiResult<ContainerTopMemoryData> res = await _apiClient.GetContainersTopByMemoryAsync(5, _shutdownToken.Token).ConfigureAwait(true);
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
            TopMemorySummaryText = ExceptionMessages.FormatForUser(ex);
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
            ApiResult<ContainerTopMemoryData> res = await _apiClient.GetContainersTopByCpuAsync(5, _shutdownToken.Token).ConfigureAwait(true);
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
            TopCpuSummaryText = ExceptionMessages.FormatForUser(ex);
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

        await RunActionAsync(() => _apiClient.StartContainerAsync(SelectedContainerRow.Model.Id, _shutdownToken.Token));
    }

    [RelayCommand]
    private async Task StopSelectedAsync()
    {
        if (SelectedContainerRow is null)
        {
            return;
        }

        await RunActionAsync(() => _apiClient.StopContainerAsync(SelectedContainerRow.Model.Id, _shutdownToken.Token));
    }

    [RelayCommand]
    private async Task RestartSelectedAsync()
    {
        if (SelectedContainerRow is null)
        {
            return;
        }

        await RunActionAsync(() => _apiClient.RestartContainerAsync(SelectedContainerRow.Model.Id, _shutdownToken.Token));
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
        DetailInspectJson = string.Empty;
        DetailStatsText = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            string id = SelectedContainerRow.Model.Id;
            Task<ApiResult<ContainerInspectData>> tInspect = _apiClient.GetContainerInspectAsync(id, _shutdownToken.Token);
            Task<ApiResult<ContainerStatsSnapshotData>> tStats = _apiClient.GetContainerStatsAsync(id, _shutdownToken.Token);
            await Task.WhenAll(tInspect, tStats).ConfigureAwait(true);
            ApiResult<ContainerInspectData> rInsp = await tInspect.ConfigureAwait(true);
            ApiResult<ContainerStatsSnapshotData> rStats = await tStats.ConfigureAwait(true);
            if (!rInsp.Success)
            {
                StatusMessage = rInsp.Error?.Message ?? "Không đọc được inspect.";
                return;
            }

            if (!rStats.Success)
            {
                StatusMessage = rStats.Error?.Message ?? "Không đọc được stats.";
                return;
            }

            DetailInspectJson = JsonSerializer.Serialize(
                rInsp.Data!.Inspect,
                new JsonSerializerOptions { WriteIndented = true });
            DetailStatsText = FormatStatsSnapshot(rStats.Data!, IsStatsRealtimeEnabled);
            StatusMessage = "Đã tải chi tiết (inspect + stats).";
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
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

    [RelayCommand]
    private async Task BatchStartAsync()
    {
        List<SelectableContainerRow> targets = FilteredItems
            .Where(r => r.IsSelected && !IsRunning(r.Model.Status))
            .ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "Không có dòng đã chọn cần start (hoặc đang chạy).";
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
                ApiResult<EmptyApiPayload> res = await _apiClient.StartContainerAsync(t.Model.Id, _shutdownToken.Token).ConfigureAwait(true);
                if (!res.Success)
                {
                    StatusMessage = res.Error?.Message ?? "Start thất bại.";
                    return;
                }
            }

            await RefreshAsync().ConfigureAwait(true);
            StatusMessage = $"Đã start {targets.Count} container.";
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
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
            StatusMessage = "Không có dòng đã chọn đang chạy để stop.";
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
                ApiResult<EmptyApiPayload> res = await _apiClient.StopContainerAsync(t.Model.Id, _shutdownToken.Token).ConfigureAwait(true);
                if (!res.Success)
                {
                    StatusMessage = res.Error?.Message ?? "Stop thất bại.";
                    return;
                }
            }

            await RefreshAsync().ConfigureAwait(true);
            StatusMessage = $"Đã stop {targets.Count} container.";
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
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
            StatusMessage = "Chọn ít nhất một container (ô chọn).";
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
                ApiResult<EmptyApiPayload> res = await _apiClient.RemoveContainerAsync(t.Model.Id, force, _shutdownToken.Token).ConfigureAwait(true);
                if (!res.Success)
                {
                    StatusMessage = res.Error?.Message ?? "Xóa thất bại.";
                    return;
                }
            }

            await RefreshAsync().ConfigureAwait(true);
            StatusMessage = $"Đã xóa {targets.Count} container.";
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
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
        await RunActionAsync(() => _apiClient.RemoveContainerAsync(SelectedContainerRow.Model.Id, force, _shutdownToken.Token));
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
                StatusMessage = string.IsNullOrWhiteSpace(res.Error?.Message) ? "Thao tác thất bại." : res.Error!.Message;
                return;
            }

            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
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
        foreach (ContainerSummaryDto item in _allItems)
        {
            if (!MatchesFilterKind(item))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(q) && !MatchesSearch(item, q))
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

        UpdateToolbarState();
    }

    private bool MatchesFilterKind(ContainerSummaryDto item)
    {
        return FilterKind switch
        {
            "Đang chạy" => IsRunning(item.Status),
            "Đã dừng" => IsStopped(item.Status),
            _ => true,
        };
    }

    private static bool MatchesSearch(ContainerSummaryDto item, string q)
    {
        return item.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
               || item.ShortId.Contains(q, StringComparison.OrdinalIgnoreCase)
               || item.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
               || item.Image.Contains(q, StringComparison.OrdinalIgnoreCase)
               || item.Status.Contains(q, StringComparison.OrdinalIgnoreCase);
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
            CanBatchStart = false;
            CanBatchStop = false;
            CanBatchRemove = false;
            return;
        }

        int n = FilteredItems.Count;
        int selectedCount = FilteredItems.Count(r => r.IsSelected);
        bool allSelected = n > 0 && selectedCount == n;
        bool anySelected = selectedCount > 0;

        CanSelectAllFiltered = n > 0 && !allSelected;
        CanClearSelection = anySelected;
        CanBatchStart = FilteredItems.Any(r => r.IsSelected && !IsRunning(r.Model.Status));
        CanBatchStop = FilteredItems.Any(r => r.IsSelected && IsRunning(r.Model.Status));
        CanBatchRemove = anySelected;
    }

    private void RestartStatsRealtimeTimerIfNeeded()
    {
        StopStatsRealtimeTimer();
        if (!IsStatsRealtimeEnabled || SelectedContainerRow is null)
        {
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
        await FetchStatsSnapshotOnceAsync().ConfigureAwait(true);
    }

    private async Task FetchStatsSnapshotOnceAsync()
    {
        if (SelectedContainerRow is null || !IsStatsRealtimeEnabled)
        {
            return;
        }

        if (!await _statsRealtimeGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            ApiResult<ContainerStatsSnapshotData> r = await _apiClient
                .GetContainerStatsAsync(SelectedContainerRow.Model.Id, _shutdownToken.Token)
                .ConfigureAwait(true);
            if (r.Success && r.Data is not null)
            {
                DetailStatsText = FormatStatsSnapshot(r.Data, true);
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
            ApiResult<ContainerStatsSnapshotData> r = await _apiClient
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
            lines.Add("(Realtime: làm mới qua API định kỳ)");
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
