using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// POST /api/containers/stats-batch — thân yêu cầu.
/// </summary>
public sealed class ContainerStatsBatchRequest
{
    [JsonPropertyName("ids")]
    public List<string> Ids { get; set; } = new();
}

/// <summary>
/// Một dòng trong phản hồi batch (ok hoặc lỗi theo từng id).
/// </summary>
public sealed class ContainerStatsBatchItemData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("stats")]
    public ContainerStatsSnapshotData? Stats { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Dữ liệu trong envelope thành công của stats-batch.
/// </summary>
public sealed class ContainerStatsBatchData
{
    [JsonPropertyName("items")]
    public List<ContainerStatsBatchItemData> Items { get; set; } = new();
}
