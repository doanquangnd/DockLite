using System.Windows;
using System.Windows.Threading;
using DockLite.App.Services;
using DockLite.Core;
using DockLite.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace DockLite.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        };

        var services = new ServiceCollection();
        services.AddDockLiteUi(AppContext.BaseDirectory);
        _serviceProvider = services.BuildServiceProvider();
        ShellCompositionResult composition = _serviceProvider.GetRequiredService<ShellCompositionResult>();
        MainWindow mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        System.Diagnostics.Debug.WriteLine(e.Exception);
        AppFileLog.WriteException("UI", e.Exception);
        MessageBox.Show(
            ExceptionMessages.FormatForUser(e.Exception),
            "DockLite",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
