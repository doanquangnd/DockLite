using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Phản hồi GET /api/docker/info (thông tin Docker Engine trong WSL).
/// </summary>
public sealed class DockerInfoResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("serverVersion")]
    public string? ServerVersion { get; init; }

    [JsonPropertyName("operatingSystem")]
    public string? OperatingSystem { get; init; }

    [JsonPropertyName("osType")]
    public string? OsType { get; init; }

    [JsonPropertyName("kernelVersion")]
    public string? KernelVersion { get; init; }

    [JsonPropertyName("containers")]
    public int Containers { get; init; }

    [JsonPropertyName("containersRunning")]
    public int ContainersRunning { get; init; }

    [JsonPropertyName("images")]
    public int Images { get; init; }
}
