using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DockLite.Contracts.Api;
using DockLite.Core.Configuration;
using DockLite.Core.Services;

namespace DockLite.Infrastructure.Api;

/// <summary>
/// WebSocket client tới ws://.../ws/containers/{id}/stats?intervalMs=...
/// </summary>
public sealed class StatsStreamClient : IStatsStreamClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly DockLiteHttpSession _session;

    public StatsStreamClient(DockLiteHttpSession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    public async Task StreamStatsAsync(
        string containerId,
        int intervalMs,
        Action<ContainerStatsSnapshotData> onSample,
        CancellationToken cancellationToken)
    {
        Uri wsUri = BuildWebSocketUri(containerId, intervalMs);
        using var ws = new ClientWebSocket();
        _session.ApplyTlsToClientWebSocketIfNeeded(ws);
        HttpClientAppSettings.CopyAuthorizationToWebSocket(ws, _session.Client);
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
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                ProcessMessage(text, onSample);
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

    private static void ProcessMessage(string text, Action<ContainerStatsSnapshotData> onSample)
    {
        using JsonDocument doc = JsonDocument.Parse(text);
        if (doc.RootElement.TryGetProperty("error", out JsonElement errEl))
        {
            string msg = errEl.GetString() ?? "Lỗi stats WebSocket";
            throw new InvalidOperationException(msg);
        }

        ContainerStatsSnapshotData? data = JsonSerializer.Deserialize<ContainerStatsSnapshotData>(text, JsonOptions);
        if (data is not null)
        {
            onSample(data);
        }
    }

    private Uri BuildWebSocketUri(string containerId, int intervalMs)
    {
        Uri? baseUri = _session.Client.BaseAddress ?? new Uri(DockLiteDefaults.ServiceBaseUrl);
        string scheme = baseUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
        string escaped = Uri.EscapeDataString(containerId);
        int ms = Math.Clamp(intervalMs, 500, 5000);
        var builder = new UriBuilder(scheme, baseUri.Host, baseUri.Port, $"/ws/containers/{escaped}/stats")
        {
            Query = $"intervalMs={ms}",
        };
        return builder.Uri;
    }
}
