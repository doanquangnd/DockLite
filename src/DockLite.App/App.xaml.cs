using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DockLite.App.Services;
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
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

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
            NetworkErrorMessageMapper.FormatForUser(e.Exception),
            UiLanguageManager.TryLocalize(Application.Current, "Ui_MainWindow_Title", "DockLite"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs args)
    {
        try
        {
            if (args.ExceptionObject is Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                AppFileLog.WriteException("AppDomain", ex);
            }
        }
        catch
        {
            // bỏ qua: lỗi ghi log không được làm hỏng quy trình tắt.
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            AppFileLog.WriteException("UnobservedTask", e.Exception);
        }
        catch
        {
            // bỏ qua
        }

        e.SetObserved();
    }
}
