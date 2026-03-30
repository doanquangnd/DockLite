using System.Windows;

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
}
