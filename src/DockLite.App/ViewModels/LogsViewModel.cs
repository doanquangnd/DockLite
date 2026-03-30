using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Models;
using DockLite.Contracts.Api;
using DockLite.Core;
using DockLite.Core.Services;

namespace DockLite.App.ViewModels;

/// <summary>
/// Xem log container: tải tail, tìm kiếm, tô mào mức log, theo dõi qua WebSocket.
/// </summary>
public partial class LogsViewModel : ObservableObject
{
    private const int MaxBufferedLines = 5000;

    private readonly IDockLiteApiClient _apiClient;
    private readonly ILogStreamClient _logStream;
    private readonly Dispatcher _dispatcher;
    private readonly List<LogLineViewModel> _buffer = new();
    private readonly StringBuilder _streamPending = new();
    private CancellationTokenSource? _followCts;

    public LogsViewModel(IDockLiteApiClient apiClient, ILogStreamClient logStream)
    {
        _apiClient = apiClient;
        _logStream = logStream;
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
            ContainerListResponse? list = await _apiClient.GetContainersAsync().ConfigureAwait(true);
            ContainerOptions.Clear();
            if (list?.Items is not null)
            {
                foreach (ContainerSummaryDto c in list.Items)
                {
                    ContainerOptions.Add(c);
                }
            }

            if (!string.IsNullOrEmpty(list?.Error))
            {
                StatusMessage = list.Error;
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
            ContainerLogsResponse? res = await _apiClient
                .GetContainerLogsAsync(SelectedContainer.Id, tail)
                .ConfigureAwait(true);
            if (res is null)
            {
                StatusMessage = "Không có phản hồi.";
                return;
            }

            if (!string.IsNullOrEmpty(res.Error))
            {
                StatusMessage = res.Error;
                return;
            }

            ReplaceBufferWithText(res.Content ?? string.Empty);
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
        CancellationToken token = _followCts.Token;
        StatusMessage = "Đang theo dõi log (WebSocket)...";
        try
        {
            await _logStream.StreamLogsAsync(
                SelectedContainer.Id,
                chunk => _dispatcher.Invoke(() => AppendStreamChunk(chunk)),
                token).ConfigureAwait(false);
            if (!token.IsCancellationRequested)
            {
                _dispatcher.Invoke(() => StatusMessage = "Luồng log đã kết thúc.");
            }
        }
        catch (OperationCanceledException)
        {
            _dispatcher.Invoke(() => StatusMessage = "Đã dừng theo dõi.");
        }
        catch (Exception ex)
        {
            string msg = ExceptionMessages.FormatForUser(ex);
            _dispatcher.Invoke(() => StatusMessage = "WebSocket: " + msg);
        }
        finally
        {
            _followCts?.Dispose();
            _followCts = null;
            _dispatcher.Invoke(() => IsFollowing = false);
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        _buffer.Clear();
        _streamPending.Clear();
        Lines.Clear();
        StatusMessage = "Đã xóa nội dung hiển thị.";
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
        foreach (string line in SplitLines(text))
        {
            AddLineToBuffer(line);
        }

        ApplySearchFilter();
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

        ApplySearchFilter();
    }

    private void AddLineToBuffer(string line)
    {
        var vm = new LogLineViewModel(line, LogLineClassifier.Classify(line));
        _buffer.Add(vm);
        while (_buffer.Count > MaxBufferedLines)
        {
            _buffer.RemoveAt(0);
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
