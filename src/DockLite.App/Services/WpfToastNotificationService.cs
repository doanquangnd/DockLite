using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace DockLite.App.Services;

/// <summary>
/// Toast trong client area cửa sổ chính (góc phải dưới), tự đóng sau vài giây; không dùng WorkArea màn hình.
/// </summary>
public sealed class WpfToastNotificationService : INotificationService
{
    private readonly MainWindowAccessor _mainWindowHost;
    private readonly object _sync = new();
    private Border? _currentToast;
    private DispatcherTimer? _closeTimer;

    public WpfToastNotificationService(MainWindowAccessor mainWindowHost)
    {
        _mainWindowHost = mainWindowHost;
    }

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

                Panel? host = ResolveToastPanel();
                if (host is null)
                {
                    return;
                }

                lock (_sync)
                {
                    RemoveToast(host, _currentToast, immediate: true);
                    Border border = BuildToastBorder(title, message, kind, host);
                    _currentToast = border;
                    host.Children.Add(border);
                    UpdateToastMaxWidth(host, border);
                    SizeChangedEventHandler onHostSizeChanged = (_, _) => UpdateToastMaxWidth(host, border);
                    host.SizeChanged += onHostSizeChanged;
                    border.Unloaded += (_, _) => host.SizeChanged -= onHostSizeChanged;

                    AnimateToastIn(border);

                    int seconds = message.Length > 1200 ? 14 : (message.Length > 400 ? 10 : 5);
                    _closeTimer?.Stop();
                    _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
                    _closeTimer.Tick += (_, _) =>
                    {
                        _closeTimer?.Stop();
                        _closeTimer = null;
                        lock (_sync)
                        {
                            RemoveToast(host, border, immediate: false);
                        }
                    };
                    _closeTimer.Start();
                }
            },
            DispatcherPriority.Normal,
            cancellationToken).Task;
    }

    private Panel? ResolveToastPanel()
    {
        Panel? p = _mainWindowHost.ToastPanel;
        if (p is not null)
        {
            return p;
        }

        if (Application.Current?.MainWindow is MainWindow mw)
        {
            return mw.ToastHostPanel;
        }

        return null;
    }

    private void RemoveToast(Panel host, Border? toast, bool immediate)
    {
        if (toast is null || !host.Children.Contains(toast))
        {
            if (ReferenceEquals(_currentToast, toast))
            {
                _currentToast = null;
            }

            return;
        }

        if (ReferenceEquals(_currentToast, toast))
        {
            _currentToast = null;
        }

        _closeTimer?.Stop();
        _closeTimer = null;

        if (immediate)
        {
            host.Children.Remove(toast);
            return;
        }

        var fade = new DoubleAnimation(toast.Opacity, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        fade.Completed += (_, _) =>
        {
            if (host.Children.Contains(toast))
            {
                host.Children.Remove(toast);
            }
        };
        toast.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private static void UpdateToastMaxWidth(Panel host, Border border)
    {
        if (host is FrameworkElement fe && fe.ActualWidth > 0)
        {
            double w = Math.Min(440, Math.Max(220, fe.ActualWidth - 8));
            border.MaxWidth = w;
        }
        else
        {
            border.MaxWidth = 440;
        }
    }

    private static void AnimateToastIn(Border border)
    {
        border.Opacity = 0;
        var tt = new TranslateTransform(0, 14);
        border.RenderTransform = tt;

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        border.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        var slide = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        tt.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private Border BuildToastBorder(string title, string message, NotificationDisplayKind kind, Panel host)
    {
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
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 8, 0, 0),
            MinWidth = 260,
            MaxHeight = 420,
            Cursor = Cursors.Hand,
            Effect = new DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 3,
                Opacity = 0.4,
                Direction = 270,
                Color = Color.FromArgb(255, 0, 0, 0),
            },
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
            Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 300,
            Content = msgBlock,
            Margin = new Thickness(0, 8, 0, 0),
        };

        var stack = new StackPanel();
        stack.Children.Add(titleBlock);
        stack.Children.Add(scroll);
        border.Child = stack;

        border.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            lock (_sync)
            {
                RemoveToast(host, border, immediate: false);
            }
        };

        return border;
    }
}
