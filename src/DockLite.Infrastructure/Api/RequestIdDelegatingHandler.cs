using System.Net.Http;

namespace DockLite.Infrastructure.Api;

/// <summary>
/// Gắn header X-Request-ID (GUID không dấu gạch) cho mỗi HTTP request tới service — khớp log req_id phía Go.
/// </summary>
internal sealed class RequestIdDelegatingHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains("X-Request-ID"))
        {
            request.Headers.TryAddWithoutValidation("X-Request-ID", Guid.NewGuid().ToString("N"));
        }

        return base.SendAsync(request, cancellationToken);
    }
}
