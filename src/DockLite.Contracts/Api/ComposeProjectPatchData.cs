using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Data PATCH compose project thành công.
/// </summary>
public sealed class ComposeProjectPatchData
{
    [JsonPropertyName("project")]
    public ComposeProjectDto? Project { get; init; }
}
