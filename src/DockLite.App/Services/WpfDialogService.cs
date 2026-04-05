using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using DockLite.App.Help;

namespace DockLite.App.Services;

/// <summary>
/// Triển khai <see cref="IDialogService"/> bằng WPF MessageBox.
/// </summary>
public sealed class WpfDialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string message, string title, DialogConfirmKind kind = DialogConfirmKind.Question)
    {
        MessageBoxImage image = kind == DialogConfirmKind.Warning ? MessageBoxImage.Warning : MessageBoxImage.Question;
        MessageBoxResult r = MessageBox.Show(message, title, MessageBoxButton.YesNo, image);
        return Task.FromResult(r == MessageBoxResult.Yes);
    }

    /// <inheritdoc />
    public Task ShowInfoAsync(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShowHelpAsync(string body, string title, IReadOnlyList<HelpHyperlink>? links = null)
    {
        var window = new Window
        {
            Title = title,
            Width = 520,
            MinHeight = 220,
            MaxHeight = 640,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current?.MainWindow,
            Padding = new Thickness(20),
        };

        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                window.Close();
                e.Handled = true;
            }
        };

        var root = new StackPanel();
        root.Children.Add(
            new TextBlock
            {
                Text = body,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
            });

        if (links is { Count: > 0 })
        {
            root.Children.Add(
                new TextBlock
                {
                    Text = UiLanguageManager.TryLocalizeCurrent("Ui_Help_LinksSectionHeader", "Liên kết"),
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 6),
                });

            foreach (HelpHyperlink link in links)
            {
                var line = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
                var hl = new Hyperlink(new Run(link.DisplayText)) { NavigateUri = link.Target };
                hl.RequestNavigate += (_, e) =>
                {
                    e.Handled = true;
                    OpenUriInBrowser(e.Uri);
                };

                line.Inlines.Add(hl);
                root.Children.Add(line);
            }
        }

        var close = new Button
        {
            Content = UiLanguageManager.TryLocalizeCurrent("Ui_Help_CloseButton", "Đóng"),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
            MinWidth = 96,
            IsDefault = true,
            IsCancel = true,
        };
        close.Click += (_, _) =>
        {
            window.DialogResult = true;
            window.Close();
        };
        root.Children.Add(close);

        // Đệm nội dung trong vùng cuộn (chữ và liên kết không dính sát viền).
        window.Content = new ScrollViewer
        {
            Content = root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(16, 12, 16, 16),
        };

        window.ShowDialog();
        return Task.CompletedTask;
    }

    private static void OpenUriInBrowser(Uri? uri)
    {
        if (uri is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // Bỏ qua (môi trường không gán trình duyệt).
        }
    }
}
