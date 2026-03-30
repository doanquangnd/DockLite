using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Nội dung data của GET /api/docker/info khi thành công.
/// </summary>
public sealed class DockerInfoData
{
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
