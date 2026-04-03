using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DockLite.App.Behaviors;

/// <summary>
/// Chuyển cuộn chuột lên ScrollViewer nội dung chính khi vùng con (ListBox/DataGrid) không còn cuộn được,
/// tránh tình trạng phải đưa chuột ra ngoài card mới cuộn được trang.
/// </summary>
public static class MouseWheelScrollBubbling
{
    public static readonly DependencyProperty BubbleToMainContentScrollProperty =
        DependencyProperty.RegisterAttached(
            "BubbleToMainContentScroll",
            typeof(bool),
            typeof(MouseWheelScrollBubbling),
            new PropertyMetadata(false, OnBubbleChanged));

    public static bool GetBubbleToMainContentScroll(DependencyObject obj) =>
        (bool)obj.GetValue(BubbleToMainContentScrollProperty);

    public static void SetBubbleToMainContentScroll(DependencyObject obj, bool value) =>
        obj.SetValue(BubbleToMainContentScrollProperty, value);

    private static void OnBubbleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement ui)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            ui.PreviewMouseWheel += OnPreviewMouseWheel;
        }
        else
        {
            ui.PreviewMouseWheel -= OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject root)
        {
            return;
        }

        if (Application.Current?.MainWindow is not Window win)
        {
            return;
        }

        if (win.FindName("MainContentScrollViewer") is not ScrollViewer outer)
        {
            return;
        }

        var inner = FindFirstDescendantScrollViewer(root);
        if (inner == null)
        {
            ScrollOuter(outer, e);
            e.Handled = true;
            return;
        }

        double scrollable = inner.ScrollableHeight;
        if (scrollable < 0.5)
        {
            ScrollOuter(outer, e);
            e.Handled = true;
            return;
        }

        double off = inner.VerticalOffset;
        bool atTop = off <= 0.01;
        bool atBottom = off >= scrollable - 0.5;
        bool wantUp = e.Delta > 0;
        if ((wantUp && atTop) || (!wantUp && atBottom))
        {
            ScrollOuter(outer, e);
            e.Handled = true;
        }
    }

    private static void ScrollOuter(ScrollViewer outer, MouseWheelEventArgs e)
    {
        outer.ScrollToVerticalOffset(outer.VerticalOffset - e.Delta / 3.0);
    }

    private static ScrollViewer? FindFirstDescendantScrollViewer(DependencyObject? d)
    {
        if (d == null)
        {
            return null;
        }

        if (d is ScrollViewer sv)
        {
            return sv;
        }

        int count = VisualTreeHelper.GetChildrenCount(d);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(d, i);
            var found = FindFirstDescendantScrollViewer(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
