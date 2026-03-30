using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Envelope JSON thống nhất: success, data, error.
/// </summary>
public sealed class ApiEnvelope<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("error")]
    public ApiErrorBody? Error { get; init; }
}
