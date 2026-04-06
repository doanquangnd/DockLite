using System.Windows;
using System.Windows.Input;
using DockLite.App.Services;

namespace DockLite.App.Views;

public partial class ImagesView
{
    public ImagesView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ShellPrimarySearchFocus.Requested += OnPrimarySearchFocusRequested;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ShellPrimarySearchFocus.Requested -= OnPrimarySearchFocusRequested;
    }

    private void OnPrimarySearchFocusRequested(object? sender, EventArgs e)
    {
        if (PrimarySearchTextBox is null)
        {
            return;
        }

        PrimarySearchTextBox.Focus();
        Keyboard.Focus(PrimarySearchTextBox);
    }
}
