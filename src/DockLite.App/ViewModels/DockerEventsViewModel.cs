using System.Text;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Services;
using DockLite.Core.Services;

namespace DockLite.App.ViewModels;

/// <summary>
/// Luồng sự kiện Docker Engine (NDJSON) — mục đích gỡ lỗi.
/// </summary>
public partial class DockerEventsViewModel : ObservableObject
{
    private const int MaxChars = 900_000;

    private readonly IDockLiteApiClient _apiClient;
    private readonly IAppShutdownToken _shutdownToken;
    private CancellationTokenSource? _streamCts;
    private readonly object _bufferLock = new();
    private readonly StringBuilder _buffer = new();

    public DockerEventsViewModel(IDockLiteApiClient apiClient, IAppShutdownToken shutdownToken)
    {
        _apiClient = apiClient;
        _shutdownToken = shutdownToken;
    }

    [ObservableProperty]
    private string _eventsText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStreaming;

    /// <summary>
    /// Bắt đầu đọc GET /api/docker/events/stream.
    /// </summary>
    [RelayCommand]
    private async Task StartStreamingAsync()
    {
        if (IsStreaming)
        {
            return;
        }

        StopStreamingInternal();
        _streamCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownToken.Token);
        CancellationToken ct = _streamCts.Token;
        IsStreaming = true;
        lock (_bufferLock)
        {
            _buffer.Clear();
        }

        SetEventsTextLocked(string.Empty);
        StatusMessage = UiLanguageManager.TryLocalizeCurrent(
            "Ui_DockerEvents_Status_Streaming",
            "Đang nhận sự kiện…");

        try
        {
            var progress = new Progress<string>(OnEventLine);
            await _apiClient.StreamDockerEventsAsync(progress, ct).ConfigureAwait(true);
            if (!ct.IsCancellationRequested)
            {
                StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                    "Ui_DockerEvents_Status_Ended",
                    "Luồng kết thúc.");
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_DockerEvents_Status_Stopped",
                "Đã dừng.");
        }
        catch (Exception ex)
        {
            StatusMessage = NetworkErrorMessageMapper.FormatForUser(ex);
        }
        finally
        {
            IsStreaming = false;
            _streamCts?.Dispose();
            _streamCts = null;
        }
    }

    private void OnEventLine(string line)
    {
        Dispatcher dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => OnEventLine(line));
            return;
        }

        lock (_bufferLock)
        {
            if (_buffer.Length > 0)
            {
                _buffer.AppendLine();
            }

            _buffer.Append(line);
            if (_buffer.Length > MaxChars)
            {
                _buffer.Remove(0, _buffer.Length - MaxChars);
            }

            EventsText = _buffer.ToString();
        }
    }

    private void SetEventsTextLocked(string text)
    {
        Application.Current?.Dispatcher.Invoke(() => EventsText = text, DispatcherPriority.Background);
    }

    /// <summary>
    /// Dừng đọc luồng (hủy token).
    /// </summary>
    [RelayCommand]
    private void StopStreaming()
    {
        StopStreamingInternal();
        StatusMessage = UiLanguageManager.TryLocalizeCurrent(
            "Ui_DockerEvents_Status_Stopped",
            "Đã dừng.");
    }

    private void StopStreamingInternal()
    {
        try
        {
            _streamCts?.Cancel();
        }
        catch
        {
            // Bỏ qua.
        }
    }

    /// <summary>
    /// Xóa vùng hiển thị.
    /// </summary>
    [RelayCommand]
    private void ClearEvents()
    {
        lock (_bufferLock)
        {
            _buffer.Clear();
        }

        EventsText = string.Empty;
    }
}
