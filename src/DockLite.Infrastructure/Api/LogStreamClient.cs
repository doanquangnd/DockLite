using System.Net.WebSockets;
using System.Text;
using DockLite.Core.Configuration;
using DockLite.Core.Services;

namespace DockLite.Infrastructure.Api;

/// <summary>
/// WebSocket client tới service Go (ws://.../ws/containers/{id}/logs).
/// </summary>
public sealed class LogStreamClient : ILogStreamClient
{
    private readonly DockLiteHttpSession _session;

    public LogStreamClient(DockLiteHttpSession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    public async Task StreamLogsAsync(string containerId, Action<string> onChunk, CancellationToken cancellationToken)
    {
        Uri wsUri = BuildWebSocketUri(containerId);
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(wsUri, cancellationToken).ConfigureAwait(false);

        var buffer = new byte[16384];
        var accumulator = new MemoryStream();

        try
        {
            while (!cancellationToken.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await ws
                    .ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                    .ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.Count > 0)
                {
                    accumulator.Write(buffer, 0, result.Count);
                }

                if (!result.EndOfMessage)
                {
                    continue;
                }

                if (accumulator.Length == 0)
                {
                    continue;
                }

                string text = Encoding.UTF8.GetString(accumulator.ToArray());
                accumulator.SetLength(0);
                if (!string.IsNullOrEmpty(text))
                {
                    onChunk(text);
                }
            }
        }
        finally
        {
            if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
            {
                await ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    string.Empty,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private Uri BuildWebSocketUri(string containerId)
    {
        Uri? baseUri = _session.Client.BaseAddress ?? new Uri(DockLiteDefaults.ServiceBaseUrl);
        string scheme = baseUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
        string escaped = Uri.EscapeDataString(containerId);
        var builder = new UriBuilder(scheme, baseUri.Host, baseUri.Port, $"/ws/containers/{escaped}/logs");
        return builder.Uri;
    }
}
