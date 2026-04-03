using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace DockLite.Contracts.Api;

/// <summary>
/// Một dòng container từ GET /api/containers.
/// </summary>
public sealed class ContainerSummaryDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("shortId")]
    public string ShortId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("image")]
    public string Image { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("ports")]
    public string? Ports { get; init; }

    [JsonPropertyName("command")]
    public string? Command { get; init; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; init; }

    /// <summary>
    /// Nhãn Docker (map key/value), từ API list containers.
    /// </summary>
    [JsonPropertyName("labels")]
    public Dictionary<string, string>? Labels { get; init; }

    /// <summary>
    /// Chuỗi nhãn rút gọn cho cột lưới (không đọc từ JSON).
    /// </summary>
    [JsonIgnore]
    public string LabelsSummary
    {
        get
        {
            if (Labels is null || Labels.Count == 0)
            {
                return string.Empty;
            }

            const int maxLen = 160;
            var sb = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in Labels.OrderBy(x => x.Key))
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(kv.Key).Append('=').Append(kv.Value);
                if (sb.Length >= maxLen)
                {
                    sb.Append('…');
                    break;
                }
            }

            return sb.ToString();
        }
    }
}
