using DockLite.App;

namespace DockLite.App.Services;

/// <summary>
/// Giữ tham chiếu tới <see cref="MainWindow"/> sau khi khởi tạo (toast và overlay gắn vào client area).
/// </summary>
public sealed class MainWindowAccessor
{
    private MainWindow? _window;

    /// <summary>
    /// Gọi từ constructor <see cref="MainWindow"/> sau <c>InitializeComponent</c>.
    /// </summary>
    public void Attach(MainWindow window)
    {
        _window = window;
    }

    /// <summary>
    /// Panel góc phải dưới để xếp toast; null nếu cửa sổ chưa gắn.
    /// </summary>
    public System.Windows.Controls.Panel? ToastPanel => _window?.ToastHostPanel;
}
