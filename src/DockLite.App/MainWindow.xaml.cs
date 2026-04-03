using System.Windows;
using DockLite.App.Services;
using DockLite.App.ViewModels;

namespace DockLite.App;

public partial class MainWindow : Window
{
    private readonly AppShellActivityState _shellActivity;

    /// <summary>
    /// Panel xếp toast (góc phải dưới); dùng bởi <see cref="MainWindowAccessor"/>.
    /// </summary>
    public System.Windows.Controls.Panel ToastHostPanel => ToastStackPanel;

    public MainWindow(
        ShellViewModel shellViewModel,
        IAppStartupService startupService,
        IAppShutdownToken shutdownToken,
        AppShellActivityState shellActivity,
        MainWindowAccessor mainWindowHost)
    {
        InitializeComponent();
        mainWindowHost.Attach(this);
        _shellActivity = shellActivity;
        DataContext = shellViewModel;
        Closed += (_, _) => shutdownToken.Cancel();
        Loaded += async (_, _) =>
        {
            UpdateShellWindowInteractiveState();
            await startupService.RunInitialLoadAsync(CancellationToken.None).ConfigureAwait(true);
        };

        Activated += (_, _) => UpdateShellWindowInteractiveState();
        Deactivated += (_, _) => UpdateShellWindowInteractiveState();
        StateChanged += (_, _) => UpdateShellWindowInteractiveState();
    }

    /// <summary>
    /// Thu nhỏ hoặc chuyển sang app khác: tạm dừng polling nền (ví dụ stats container).
    /// </summary>
    private void UpdateShellWindowInteractiveState()
    {
        bool interactive = WindowState != WindowState.Minimized && IsActive;
        _shellActivity.SetMainWindowInteractive(interactive);
    }
}
