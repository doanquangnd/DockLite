using System.Windows.Threading;

namespace DockLite.App.Services;

/// <summary>
/// Debounce ô tìm kiếm (mặc định 250 ms) — tránh lọc lại mỗi phím trên bảng lớn.
/// </summary>
public sealed class SearchDebounceHelper
{
    private readonly Action _action;
    private readonly int _delayMs;
    private DispatcherTimer? _timer;

    public SearchDebounceHelper(Action action, int delayMs = 250)
    {
        _action = action;
        _delayMs = delayMs;
    }

    /// <summary>
    /// Khởi động lại bộ đếm; sau khoảng trễ đã cấu hình mà không có thêm lần gọi sẽ chạy thao tác lọc trên UI thread.
    /// </summary>
    public void Schedule()
    {
        if (_timer is null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_delayMs) };
            _timer.Tick += (_, _) =>
            {
                _timer!.Stop();
                _action();
            };
        }
        else
        {
            _timer.Stop();
        }

        _timer.Start();
    }
}
