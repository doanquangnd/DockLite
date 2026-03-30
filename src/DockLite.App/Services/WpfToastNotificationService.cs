using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DockLite.App.Services;

/// <summary>
/// Toast góc màn hình (WPF), tự đóng sau vài giây; nội dung dài có cuộn.
/// </summary>
public sealed class WpfToastNotificationService : INotificationService
{
    private readonly object _sync = new();
    private Window? _currentToast;

    /// <inheritdoc />
    public Task ShowInfoAsync(string title, string message, CancellationToken cancellationToken = default) =>
        ShowAsync(title, message, NotificationDisplayKind.Info, cancellationToken);

    /// <inheritdoc />
    public Task ShowAsync(
        string title,
        string message,
        NotificationDisplayKind kind = NotificationDisplayKind.Info,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        Application? app = Application.Current;
        if (app is null)
        {
            return Task.CompletedTask;
        }

        return app.Dispatcher.InvokeAsync(
            () =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                lock (_sync)
                {
                    _currentToast?.Close();
                    var toast = new ToastNotificationWindow(title, message, kind);
                    _currentToast = toast;
                    toast.Closed += (_, _) =>
                    {
                        lock (_sync)
                        {
                            if (ReferenceEquals(_currentToast, toast))
                            {
                                _currentToast = null;
                            }
                        }
                    };

                    toast.Show();
                }
            },
            DispatcherPriority.Normal,
            cancellationToken).Task;
    }

    /// <summary>
    /// Cửa sổ toast tối giản, không taskbar, không cướp focus.
    /// </summary>
    private sealed class ToastNotificationWindow : Window
    {
        private readonly DispatcherTimer _timer;

        public ToastNotificationWindow(string title, string message, NotificationDisplayKind kind)
        {
            Title = title;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            Width = 440;
            MaxHeight = 420;
            SizeToContent = SizeToContent.Height;

            (Color bg, Color edge) = kind switch
            {
                NotificationDisplayKind.Warning => (Color.FromArgb(235, 58, 48, 28), Color.FromArgb(255, 200, 120, 40)),
                NotificationDisplayKind.Success => (Color.FromArgb(235, 28, 52, 40), Color.FromArgb(255, 60, 160, 90)),
                _ => (Color.FromArgb(235, 42, 42, 42), Color.FromArgb(255, 80, 80, 80)),
            };

            var border = new Border
            {
                Background = new SolidColorBrush(bg),
                BorderBrush = new SolidColorBrush(edge),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
            };

            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
            };

            var msgBlock = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            };

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 300,
                Content = msgBlock,
                Margin = new Thickness(0, 6, 0, 0),
            };

            var stack = new StackPanel();
            stack.Children.Add(titleBlock);
            stack.Children.Add(scroll);
            border.Child = stack;
            Content = border;

            ContentRendered += OnContentRendered;
            MouseLeftButtonDown += (_, _) => Close();

            int seconds = message.Length > 1200 ? 14 : (message.Length > 400 ? 10 : 5);
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            _timer.Tick += (_, _) =>
            {
                _timer.Stop();
                Close();
            };
            _timer.Start();
        }

        private void OnContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= OnContentRendered;
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 16;
            Top = wa.Bottom - ActualHeight - 16;
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            base.OnClosed(e);
        }
    }
}
