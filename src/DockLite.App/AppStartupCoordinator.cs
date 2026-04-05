using DockLite.App.Services;
using DockLite.Core.Diagnostics;
using DockLite.Infrastructure.Wsl;

namespace DockLite.App;

/// <summary>
/// Gom luồng khởi động sau khi cửa sổ chính load (WSL auto-start, refresh dashboard).
/// </summary>
public sealed class AppStartupCoordinator : IAppStartupService
{
    private readonly ShellCompositionResult _composition;
    private readonly AppHostContext _hostContext;
    private readonly INotificationService _notificationService;
    private readonly IAppShutdownToken _shutdownToken;

    public AppStartupCoordinator(
        ShellCompositionResult composition,
        AppHostContext hostContext,
        INotificationService notificationService,
        IAppShutdownToken shutdownToken)
    {
        _composition = composition;
        _hostContext = hostContext;
        _notificationService = notificationService;
        _shutdownToken = shutdownToken;
    }

    /// <inheritdoc />
    public async Task RunInitialLoadAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _shutdownToken.Token);
        try
        {
            var progress = new Progress<WslStartupProgress>(p => _composition.Shell.ApplyWslStartupProgress(p));
            (bool ok, WslEnsureFailureReason reason) = await WslDockerServiceAutoStart.TryEnsureRunningAsync(
                _composition.HttpSession,
                _composition.Settings,
                _hostContext.BaseDirectory,
                linked.Token,
                progress).ConfigureAwait(true);

            if (DiagnosticTelemetry.IsEnabled)
            {
                DiagnosticTelemetry.WriteEvent(
                    "app_startup_ensure_finished",
                    ("ok", ok.ToString()),
                    ("reason", reason.ToString()),
                    ("base_url", DiagnosticTelemetry.FormatBaseUrlForTelemetry(_composition.Settings.ServiceBaseUrl)));
            }

            if (!ok && reason == WslEnsureFailureReason.HealthTimeoutAfterWslStart)
            {
                string body = WslDockerServiceAutoStart.FormatHealthTimeoutUserHint(AppFileLog.LogDirectory);
                await _notificationService
                    .ShowAsync("DockLite — health timeout", body, NotificationDisplayKind.Warning, linked.Token)
                    .ConfigureAwait(true);
            }

            await _composition.Shell.RefreshServiceHeaderFromApiAsync(linked.Token).ConfigureAwait(true);

            // Probe ban đầu có thể OK trong khi GET health qua API client vẫn lỗi (WSL resume / TCP); một lần restart + chờ.
            if (_composition.Settings.AutoStartWslService && _composition.HealthCache.LastHealthy != true)
            {
                var recoveryProgress = new Progress<WslStartupProgress>(p => _composition.Shell.ApplyWslStartupProgress(p));
                (bool recoveryOk, WslEnsureFailureReason recoveryReason) =
                    await WslDockerServiceAutoStart.TrySpawnWslRestartAndWaitForHealthAsync(
                        _composition.HttpSession,
                        _composition.Settings,
                        _hostContext.BaseDirectory,
                        linked.Token,
                        recoveryProgress).ConfigureAwait(true);

                if (DiagnosticTelemetry.IsEnabled)
                {
                    DiagnosticTelemetry.WriteEvent(
                        "app_startup_recovery_ensure_finished",
                        ("ok", recoveryOk.ToString()),
                        ("reason", recoveryReason.ToString()),
                        ("base_url", DiagnosticTelemetry.FormatBaseUrlForTelemetry(_composition.Settings.ServiceBaseUrl)));
                }

                if (!recoveryOk && recoveryReason == WslEnsureFailureReason.HealthTimeoutAfterWslStart)
                {
                    string recoveryBody = WslDockerServiceAutoStart.FormatHealthTimeoutUserHint(AppFileLog.LogDirectory);
                    await _notificationService
                        .ShowAsync("DockLite — health timeout", recoveryBody, NotificationDisplayKind.Warning, linked.Token)
                        .ConfigureAwait(true);
                }

                await _composition.Shell.RefreshServiceHeaderFromApiAsync(linked.Token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Đóng cửa sổ trong lúc chờ WSL/health — không làm mới dashboard.
            return;
        }

        await _composition.Shell.Dashboard.RefreshCommand.ExecuteAsync(null);
    }
}
