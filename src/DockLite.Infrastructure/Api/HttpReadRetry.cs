using System.Net.Http;
using DockLite.Contracts.Api;
using DockLite.Core.Diagnostics;

namespace DockLite.Infrastructure.Api;

/// <summary>
/// Thử lại thao tác đọc HTTP khi lỗi mạng tạm thời (không dùng cho POST/DELETE có tác dụng phụ).
/// </summary>
internal static class HttpReadRetry
{
    private const int MaxAttempts = 3;

    /// <summary>
    /// Dùng cho GET không envelope (ví dụ /api/health).
    /// </summary>
    internal static async Task<T?> ExecuteNullableAsync<T>(
        Func<Task<T?>> action,
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(400);
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                if (attempt < MaxAttempts)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 4000));
                    continue;
                }

                DiagnosticTelemetry.WriteHttpReadExhausted(nameof(HttpRequestException), MaxAttempts);
                throw;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt < MaxAttempts)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 4000));
                    continue;
                }

                DiagnosticTelemetry.WriteHttpReadExhausted("TaskCanceled_NotUser", MaxAttempts);
                throw;
            }
        }

        throw new InvalidOperationException("HttpReadRetry: vòng lặp không mong đợi.");
    }

    internal static async Task<ApiResult<T>> ExecuteAsync<T>(
        Func<Task<ApiResult<T>>> action,
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(400);
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                if (attempt < MaxAttempts)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 4000));
                    continue;
                }

                DiagnosticTelemetry.WriteHttpReadExhausted(nameof(HttpRequestException), MaxAttempts);
                throw;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt < MaxAttempts)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 4000));
                    continue;
                }

                DiagnosticTelemetry.WriteHttpReadExhausted("TaskCanceled_NotUser", MaxAttempts);
                throw;
            }
        }

        throw new InvalidOperationException("HttpReadRetry: vòng lặp không mong đợi.");
    }
}
