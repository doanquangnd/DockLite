using System.Windows;
using DockLite.App.Services;
using DockLite.App.ViewModels;

namespace DockLite.App;

public partial class MainWindow : Window
{
    public MainWindow(
        ShellViewModel shellViewModel,
        IAppStartupService startupService,
        IAppShutdownToken shutdownToken)
    {
        InitializeComponent();
        DataContext = shellViewModel;
        Closed += (_, _) => shutdownToken.Cancel();
        Loaded += async (_, _) =>
        {
            await startupService.RunInitialLoadAsync(CancellationToken.None).ConfigureAwait(true);
        };
    }
}
