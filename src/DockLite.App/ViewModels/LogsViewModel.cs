using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Models;
using DockLite.App.Services;
using DockLite.Contracts.Api;
using DockLite.Core.Services;

namespace DockLite.App.ViewModels;

/// <summary>
/// Xem log container: tải tail, tìm kiếm, tô màu mức log, theo dõi qua WebSocket.
/// </summary>
public partial class LogsViewModel : ObservableObject
{
    private const int MaxBufferedLines = 5000;

    /// <summary>
    /// Tìm trong ô lọc: với dòng dài hơn ngưỡng chỉ quét tiền tố (tránh Contains trên chuỗi hàng MB).
    /// </summary>
    private const int MaxLogLineSearchChars = 65536;

    private const int FollowFlushIntervalMsDefault = 150;
    private const int FollowFlushIntervalMsMedium = 280;
    private const int FollowFlushIntervalMsSlow = 450;

    /// <summary>
    /// Giới hạn thời gian chờ khi tải danh sách container (tránh chờ trùng với timeout HTTP dài trong Cài đặt khi service không phản hồi).
    /// </summary>
    private static readonly TimeSpan _listRequestTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogsScreenApi _logsApi;
    private readonly ILogStreamClient _logStream;
    private readonly INotificationService _notificationService;
    private readonly IAppShutdownToken _shutdownToken;
    private readonly AppShellActivityState _shellActivity;
    private readonly Dispatcher _dispatcher;
    private readonly List<LogLineViewModel> _buffer = new();
    private readonly StringBuilder _streamPending = new();
    private readonly StringBuilder _incomingStreamChunks = new();
    private readonly object _streamChunkLock = new();
    private DispatcherTimer? _followFlushTimer;
    private CancellationTokenSource? _followCts;
    private readonly SearchDebounceHelper _searchDebounce;
    private readonly SemaphoreSlim _loadContainersGate = new(1, 1);

    public LogsViewModel(
        ILogsScreenApi logsApi,
        ILogStreamClient logStream,
        INotificationService notificationService,
        IAppShutdownToken shutdownToken,
        AppShellActivityState shellActivity)
    {
        _logsApi = logsApi;
        _logStream = logStream;
        _notificationService = notificationService;
        _shutdownToken = shutdownToken;
        _shellActivity = shellActivity;
        _dispatcher = Application.Current.Dispatcher;
        _searchDebounce = new SearchDebounceHelper(ApplySearchFilter);
        _shellActivity.Changed += OnShellActivityChangedForLogs;
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyEmptyLogContainerHint();
    }

    private void OnShellActivityChangedForLogs(object? sender, EventArgs e)
    {
        // Rời tab Log: hủy WebSocket follow để giảm tải mạng (quay lại tab có thể bật follow lại).
        if (IsFollowing && !_shellActivity.IsLogsPageVisible)
        {
            StopFollow();
        }

        SyncFollowFlushTimerWithShellActivity();
    }

    /// <summary>
    /// Khởi động lại timer flush khi đang follow và điều kiện tab/cửa sổ cho phép; tạm dừng timer khi không (chunk vẫn tích lũy trong bộ đệm).
    /// </summary>
    private void SyncFollowFlushTimerWithShellActivity()
    {
        if (!IsFollowing)
        {
            return;
        }

        if (_shellActivity.ShouldProcessLogsFollowFlush)
        {
            if (_followFlushTimer is null)
            {
                StartFollowFlushTimer();
            }
        }
        else
        {
            StopFollowFlushTimer();
        }
    }

    private void StartFollowFlushTimer()
    {
        StopFollowFlushTimer();
        if (!_shellActivity.ShouldProcessLogsFollowFlush)
        {
            return;
        }

        _followFlushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FollowFlushIntervalMsDefault) };
        _followFlushTimer.Tick += OnFollowFlushTick;
        _followFlushTimer.Start();
    }

    public ObservableCollection<ContainerSummaryDto> ContainerOptions { get; } = new();

    /// <summary>Không có container để chọn (sau khi tải xong).</summary>
    public bool ShowEmptyContainerOptionsHint => !IsBusy && ContainerOptions.Count == 0;

    private void NotifyEmptyLogContainerHint()
    {
        OnPropertyChanged(nameof(ShowEmptyContainerOptionsHint));
    }

    [ObservableProperty]
    private ContainerSummaryDto? _selectedContainer;

    [ObservableProperty]
    private int _tailLines = 200;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isFollowing;

    public ObservableCollection<LogLineViewModel> Lines { get; } = new();

    partial void OnSelectedContainerChanged(ContainerSummaryDto? value)
    {
        if (IsFollowing)
        {
            StopFollow();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounce.Schedule();
    }

    /// <summary>
    /// Tải danh sách container cho ComboBox (gọi khi vào trang Log).
    /// </summary>
    [RelayCommand]
    private async Task LoadContainersAsync()
    {
        if (!_shellActivity.ShouldRefreshLogsContainerList)
        {
            return;
        }

        if (!await _loadContainersGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Logs_Status_LoadingContainers",
                "Đang tải danh sách container...");
            try
            {
                using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownToken.Token);
                requestCts.CancelAfter(_listRequestTimeout);

                ApiResult<ContainerListData> list = await _logsApi.GetContainersAsync(requestCts.Token).ConfigureAwait(true);
                ContainerOptions.Clear();
                if (!list.Success)
                {
                    string err = list.Error?.Message
                        ?? UiLanguageManager.TryLocalizeCurrent("Ui_Status_Common_ListLoadFailed", "Không đọc được danh sách.");
                    StatusMessage = err;
                    _ = ApiErrorUiFeedback.ShowWarningToastAsync(_notificationService, err);
                }
                else if (list.Data?.Items is not null)
                {
                    foreach (ContainerSummaryDto c in list.Data.Items)
                    {
                        ContainerOptions.Add(c);
                    }

                    StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                        "Ui_Status_Common_LoadedContainersCountFormat",
                        "Đã tải {0} container.",
                        ContainerOptions.Count);
                }
                else
                {
                    StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                        "Ui_Status_Common_LoadedContainersCountFormat",
                        "Đã tải {0} container.",
                        ContainerOptions.Count);
                }
            }
            catch (OperationCanceledException)
            {
                if (_shutdownToken.Token.IsCancellationRequested)
                {
                    throw;
                }

                StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Logs_Status_ListRequestTimeout",
                    "Hết thời gian chờ khi tải danh sách container. Kiểm tra service hoặc mạng.");
            }
            catch (Exception ex)
            {
                StatusMessage = NetworkErrorMessageMapper.FormatForUser(ex);
                _ = ApiErrorUiFeedback.ShowNetworkExceptionToastAsync(_notificationService, ex);
            }
            finally
            {
                IsBusy = false;
                NotifyEmptyLogContainerHint();
            }
        }
        finally
        {
            _loadContainersGate.Release();
        }
    }

    [RelayCommand]
    private async Task LoadTailAsync()
    {
        if (SelectedContainer is null)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Logs_Status_SelectContainer", "Chọn container.");
            return;
        }

        int tail = TailLines < 1 ? 200 : Math.Min(TailLines, 10000);
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            ApiResult<ContainerLogsData> res = await _logsApi
                .GetContainerLogsAsync(SelectedContainer.Id, tail, _shutdownToken.Token)
                .ConfigureAwait(true);
            if (!res.Success)
            {
                string err = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Logs_Status_NoResponse", "Không có phản hồi.");
                StatusMessage = err;
                _ = ApiErrorUiFeedback.ShowWarningToastAsync(_notificationService, err);
                return;
            }

            ReplaceBufferWithText(res.Data?.Content ?? string.Empty);
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Logs_Status_TailLoaded", "Đã tải log (tail).");
        }
        catch (Exception ex)
        {
            StatusMessage = NetworkErrorMessageMapper.FormatForUser(ex);
            _ = ApiErrorUiFeedback.ShowNetworkExceptionToastAsync(_notificationService, ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleFollowAsync()
    {
        if (SelectedContainer is null)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Logs_Status_SelectContainer", "Chọn container.");
            return;
        }

        if (IsFollowing)
        {
            StopFollow();
            return;
        }

        IsFollowing = true;
        _followCts = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_followCts.Token, _shutdownToken.Token);
        CancellationToken token = linked.Token;
        StatusMessage = UiLanguageManager.TryLocalizeCurrent(
            "Ui_Logs_Status_FollowWsProgress",
            "Đang theo dõi log (WebSocket)...");

        StartFollowFlushTimer();

        try
        {
            await _logStream.StreamLogsAsync(
                SelectedContainer.Id,
                chunk =>
                {
                    lock (_streamChunkLock)
                    {
                        _incomingStreamChunks.Append(chunk);
                    }
                },
                token).ConfigureAwait(false);
            if (!token.IsCancellationRequested)
            {
                _ = _dispatcher.BeginInvoke(
                    () => StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                        "Ui_Logs_Status_FollowEnded",
                        "Luồng log đã kết thúc."),
                    DispatcherPriority.Background);
            }
        }
        catch (OperationCanceledException)
        {
            _ = _dispatcher.BeginInvoke(
                () => StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Logs_Status_FollowStopped",
                    "Đã dừng theo dõi."),
                DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            string msg = NetworkErrorMessageMapper.FormatForUser(ex);
            _ = _dispatcher.BeginInvoke(
                () =>
                {
                    StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                        "Ui_Logs_Status_WebSocketPrefixFormat",
                        "WebSocket: {0}",
                        msg);
                    _ = ApiErrorUiFeedback.ShowNetworkExceptionToastAsync(_notificationService, ex);
                },
                DispatcherPriority.Background);
        }
        finally
        {
            StopFollowFlushTimer();
            string tail;
            lock (_streamChunkLock)
            {
                tail = _incomingStreamChunks.ToString();
                _incomingStreamChunks.Clear();
            }

            _dispatcher.Invoke(() =>
            {
                if (tail.Length > 0)
                {
                    AppendStreamChunk(tail);
                }

                _followCts?.Dispose();
                _followCts = null;
                IsFollowing = false;
            });
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        _buffer.Clear();
        _streamPending.Clear();
        lock (_streamChunkLock)
        {
            _incomingStreamChunks.Clear();
        }

        Lines.Clear();
        StatusMessage = UiLanguageManager.TryLocalizeCurrent(
            "Ui_Logs_Status_ClearedDisplay",
            "Đã xóa nội dung hiển thị.");
    }

    private void OnFollowFlushTick(object? sender, EventArgs e)
    {
        if (!_shellActivity.ShouldProcessLogsFollowFlush)
        {
            return;
        }

        string batch;
        lock (_streamChunkLock)
        {
            batch = _incomingStreamChunks.ToString();
            _incomingStreamChunks.Clear();
        }

        if (batch.Length == 0)
        {
            long pendingAfter;
            lock (_streamChunkLock)
            {
                pendingAfter = _incomingStreamChunks.Length;
            }

            if (_followFlushTimer is not null && _buffer.Count < 2200 && pendingAfter < 50_000)
            {
                _followFlushTimer.Interval = TimeSpan.FromMilliseconds(FollowFlushIntervalMsDefault);
            }

            return;
        }

        var sw = Stopwatch.StartNew();
        AppendStreamChunk(batch);
        sw.Stop();
        AdjustFollowFlushIntervalAfterWork(sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Giảm tần suất flush khi bộ đệm lớn hoặc một tick xử lý lâu (heuristic thay cho đo FPS).
    /// </summary>
    private void AdjustFollowFlushIntervalAfterWork(long elapsedMsThisTick)
    {
        if (_followFlushTimer is null)
        {
            return;
        }

        int backlog = _buffer.Count;
        long pendingLen;
        lock (_streamChunkLock)
        {
            pendingLen = _incomingStreamChunks.Length;
        }

        int targetMs = FollowFlushIntervalMsDefault;
        if (elapsedMsThisTick >= 75 || backlog >= 4500 || pendingLen >= 400_000)
        {
            targetMs = FollowFlushIntervalMsSlow;
        }
        else if (elapsedMsThisTick >= 35 || backlog >= 2800 || pendingLen >= 120_000)
        {
            targetMs = FollowFlushIntervalMsMedium;
        }

        double cur = _followFlushTimer.Interval.TotalMilliseconds;
        if (Math.Abs(cur - targetMs) > 0.5)
        {
            _followFlushTimer.Interval = TimeSpan.FromMilliseconds(targetMs);
        }
    }

    private static bool LineMatchesSearch(string line, string q)
    {
        if (string.IsNullOrEmpty(q))
        {
            return true;
        }

        if (line.Length <= MaxLogLineSearchChars)
        {
            return line.Contains(q, StringComparison.OrdinalIgnoreCase);
        }

        return line.AsSpan(0, MaxLogLineSearchChars).Contains(q.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private void StopFollowFlushTimer()
    {
        if (_followFlushTimer is null)
        {
            return;
        }

        _followFlushTimer.Stop();
        _followFlushTimer.Tick -= OnFollowFlushTick;
        _followFlushTimer = null;
    }

    private void StopFollow()
    {
        try
        {
            _followCts?.Cancel();
        }
        catch
        {
            // bỏ qua
        }
    }

    private void ReplaceBufferWithText(string text)
    {
        _buffer.Clear();
        _streamPending.Clear();
        Lines.Clear();
        foreach (string line in SplitLines(text))
        {
            AddLineToBuffer(line);
        }
    }

    private void AppendStreamChunk(string chunk)
    {
        _streamPending.Append(chunk);
        string s = _streamPending.ToString();
        int lastNl = s.LastIndexOfAny(new[] { '\r', '\n' });
        if (lastNl < 0)
        {
            return;
        }

        string complete = s[..(lastNl + 1)];
        _streamPending.Clear();
        if (lastNl + 1 < s.Length)
        {
            _streamPending.Append(s[(lastNl + 1)..]);
        }

        foreach (string line in SplitLines(complete))
        {
            if (line.Length == 0)
            {
                continue;
            }

            AddLineToBuffer(line);
        }
    }

    private void AddLineToBuffer(string line)
    {
        var vm = new LogLineViewModel(line, LogLineClassifier.Classify(line));
        _buffer.Add(vm);
        string q = SearchText.Trim();

        while (_buffer.Count > MaxBufferedLines)
        {
            LogLineViewModel removed = _buffer[0];
            _buffer.RemoveAt(0);
            int idx = Lines.IndexOf(removed);
            if (idx >= 0)
            {
                Lines.RemoveAt(idx);
            }
        }

        if (string.IsNullOrEmpty(q))
        {
            Lines.Add(vm);
            return;
        }

        if (LineMatchesSearch(vm.Text, q))
        {
            Lines.Add(vm);
        }
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            yield return line;
        }
    }

    private void ApplySearchFilter()
    {
        Lines.Clear();
        string q = SearchText.Trim();
        foreach (LogLineViewModel line in _buffer)
        {
            if (string.IsNullOrEmpty(q) || LineMatchesSearch(line.Text, q))
            {
                Lines.Add(line);
            }
        }
    }
}
