using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// POST /api/compose/config/services — danh sách tên service từ compose file.
/// </summary>
public sealed class ComposeServiceListData
{
    [JsonPropertyName("items")]
    public List<string> Items { get; init; } = new();

    [JsonPropertyName("output")]
    public string? Output { get; init; }
}
