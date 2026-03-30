namespace DockLite.App.Services;

/// <summary>
/// Triển khai <see cref="IAppShutdownToken"/> bằng <see cref="CancellationTokenSource"/>.
/// </summary>
public sealed class AppShutdownToken : IAppShutdownToken
{
    private readonly CancellationTokenSource _cts = new();

    /// <inheritdoc />
    public CancellationToken Token => _cts.Token;

    /// <inheritdoc />
    public void Cancel()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Đã dispose khi thoát ứng dụng.
        }
    }
}
