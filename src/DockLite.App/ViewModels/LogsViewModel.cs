using System.Collections.ObjectModel;
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
using DockLite.Core;
using DockLite.Core.Services;

namespace DockLite.App.ViewModels;

/// <summary>
/// Xem log container: tải tail, tìm kiếm, tô màu mức log, theo dõi qua WebSocket.
/// </summary>
public partial class LogsViewModel : ObservableObject
{
    private const int MaxBufferedLines = 5000;
    private static readonly TimeSpan FollowFlushInterval = TimeSpan.FromMilliseconds(150);

    private readonly IDockLiteApiClient _apiClient;
    private readonly ILogStreamClient _logStream;
    private readonly IAppShutdownToken _shutdownToken;
    private readonly Dispatcher _dispatcher;
    private readonly List<LogLineViewModel> _buffer = new();
    private readonly StringBuilder _streamPending = new();
    private readonly StringBuilder _incomingStreamChunks = new();
    private readonly object _streamChunkLock = new();
    private DispatcherTimer? _followFlushTimer;
    private CancellationTokenSource? _followCts;

    public LogsViewModel(IDockLiteApiClient apiClient, ILogStreamClient logStream, IAppShutdownToken shutdownToken)
    {
        _apiClient = apiClient;
        _logStream = logStream;
        _shutdownToken = shutdownToken;
        _dispatcher = Application.Current.Dispatcher;
    }

    public ObservableCollection<ContainerSummaryDto> ContainerOptions { get; } = new();

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
        ApplySearchFilter();
    }

    /// <summary>
    /// Tải danh sách container cho ComboBox (gọi khi vào trang Log).
    /// </summary>
    [RelayCommand]
    private async Task LoadContainersAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            ApiResult<ContainerListData> list = await _apiClient.GetContainersAsync(_shutdownToken.Token).ConfigureAwait(true);
            ContainerOptions.Clear();
            if (!list.Success)
            {
                StatusMessage = list.Error?.Message ?? "Không đọc được danh sách.";
            }
            else if (list.Data?.Items is not null)
            {
                foreach (ContainerSummaryDto c in list.Data.Items)
                {
                    ContainerOptions.Add(c);
                }

                StatusMessage = $"Đã tải {ContainerOptions.Count} container.";
            }
            else
            {
                StatusMessage = $"Đã tải {ContainerOptions.Count} container.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadTailAsync()
    {
        if (SelectedContainer is null)
        {
            StatusMessage = "Chọn container.";
            return;
        }

        int tail = TailLines < 1 ? 200 : Math.Min(TailLines, 10000);
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            ApiResult<ContainerLogsData> res = await _apiClient
                .GetContainerLogsAsync(SelectedContainer.Id, tail, _shutdownToken.Token)
                .ConfigureAwait(true);
            if (!res.Success)
            {
                StatusMessage = res.Error?.Message ?? "Không có phản hồi.";
                return;
            }

            ReplaceBufferWithText(res.Data?.Content ?? string.Empty);
            StatusMessage = "Đã tải log (tail).";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
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
            StatusMessage = "Chọn container.";
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
        StatusMessage = "Đang theo dõi log (WebSocket)...";

        _followFlushTimer = new DispatcherTimer { Interval = FollowFlushInterval };
        _followFlushTimer.Tick += OnFollowFlushTick;
        _followFlushTimer.Start();

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
                    () => StatusMessage = "Luồng log đã kết thúc.",
                    DispatcherPriority.Background);
            }
        }
        catch (OperationCanceledException)
        {
            _ = _dispatcher.BeginInvoke(
                () => StatusMessage = "Đã dừng theo dõi.",
                DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            string msg = ExceptionMessages.FormatForUser(ex);
            _ = _dispatcher.BeginInvoke(
                () => StatusMessage = "WebSocket: " + msg,
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
        StatusMessage = "Đã xóa nội dung hiển thị.";
    }

    private void OnFollowFlushTick(object? sender, EventArgs e)
    {
        string batch;
        lock (_streamChunkLock)
        {
            batch = _incomingStreamChunks.ToString();
            _incomingStreamChunks.Clear();
        }

        if (batch.Length == 0)
        {
            return;
        }

        AppendStreamChunk(batch);
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

        if (vm.Text.Contains(q, StringComparison.OrdinalIgnoreCase))
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
            if (string.IsNullOrEmpty(q) || line.Text.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                Lines.Add(line);
            }
        }
    }
}
