using System.Windows;
using System.Windows.Threading;
using DockLite.Core;
using DockLite.Core.Diagnostics;

namespace DockLite.App;

public partial class App : Application
{
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
