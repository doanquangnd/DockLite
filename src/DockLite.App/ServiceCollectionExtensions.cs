using DockLite.App.Services;
using DockLite.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DockLite.App;

/// <summary>
/// Đăng ký DI cho UI WPF (composition, cửa sổ chính).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký shell, <see cref="ShellCompositionResult"/>, <see cref="ShellViewModel"/> và <see cref="MainWindow"/>.
    /// </summary>
    public static IServiceCollection AddDockLiteUi(this IServiceCollection services, string appBaseDirectory)
    {
        services.AddSingleton(new AppHostContext(appBaseDirectory));
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<INotificationService, WpfToastNotificationService>();
        services.AddSingleton<IAppShutdownToken, AppShutdownToken>();
        services.AddSingleton<AppUiDisplaySettings>();
        services.AddSingleton<IAppShellFactory, AppShellFactory>();
        services.AddSingleton<ShellCompositionResult>(sp =>
        {
            IAppShellFactory factory = sp.GetRequiredService<IAppShellFactory>();
            string baseDir = sp.GetRequiredService<AppHostContext>().BaseDirectory;
            return factory.Create(baseDir);
        });
        services.AddSingleton<ShellViewModel>(sp => sp.GetRequiredService<ShellCompositionResult>().Shell);
        services.AddSingleton<IAppStartupService, AppStartupCoordinator>();
        services.AddSingleton<MainWindow>();
        return services;
    }
}
