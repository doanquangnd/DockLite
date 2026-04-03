using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Một project Compose đã lưu trên service WSL.
/// </summary>
public sealed class ComposeProjectDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("wslPath")]
    public string WslPath { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Đường dẫn tương đối trong thư mục project cho từng <c>docker compose -f</c>; rỗng = tự tìm compose mặc định.
    /// </summary>
    [JsonPropertyName("composeFiles")]
    public List<string>? ComposeFiles { get; init; }

    /// <summary>
    /// Hiển thị trong lưới; không nằm trong JSON từ server.
    /// </summary>
    [JsonIgnore]
    public string ComposeFilesSummary =>
        ComposeFiles is null || ComposeFiles.Count == 0
            ? "(mặc định)"
            : string.Join(", ", ComposeFiles);
}
