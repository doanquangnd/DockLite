using System.Text;
using System.Text.Json;

namespace DockLite.App.Services;

/// <summary>
/// Chuyển JSON inspect của Docker Engine thành văn bản tóm tắt (đọc được trước khi xem JSON thô).
/// </summary>
public static class ContainerInspectSummaryFormatter
{
    private const int MaxEnvLines = 24;

    /// <summary>
    /// Tạo chuỗi nhiều dòng mô tả các trường thường dùng; không ném ngoại lệ ra ngoài.
    /// </summary>
    public static string Format(JsonElement inspect)
    {
        try
        {
            return FormatCore(inspect);
        }
        catch
        {
            return "Không đọc được cấu trúc inspect (JSON không đúng dạng mong đợi).";
        }
    }

    private static string FormatCore(JsonElement root)
    {
        var sb = new StringBuilder();

        if (TryGetString(root, "Id", out string? id) && !string.IsNullOrEmpty(id))
        {
            sb.Append("Id: ").AppendLine(id.Length > 16 ? id[..16] + "…" : id);
        }

        if (TryGetString(root, "Name", out string? name) && name is not null)
        {
            sb.Append("Tên: ").AppendLine(name.TrimStart('/'));
        }

        if (root.TryGetProperty("State", out JsonElement state))
        {
            if (TryGetString(state, "Status", out string? st))
            {
                sb.Append("Trạng thái: ").AppendLine(st);
            }

            if (state.TryGetProperty("Running", out JsonElement running) && running.ValueKind == JsonValueKind.True)
            {
                if (TryGetString(state, "StartedAt", out string? started) && !string.IsNullOrEmpty(started))
                {
                    sb.Append("Bắt đầu: ").AppendLine(started);
                }
            }
        }

        if (root.TryGetProperty("Config", out JsonElement config))
        {
            if (TryGetString(config, "Image", out string? img))
            {
                sb.Append("Image: ").AppendLine(img);
            }

            if (TryGetString(config, "Hostname", out string? host) && !string.IsNullOrEmpty(host))
            {
                sb.Append("Hostname: ").AppendLine(host);
            }

            AppendEnvBlock(sb, config);
        }

        if (TryGetString(root, "Created", out string? created) && !string.IsNullOrEmpty(created))
        {
            sb.Append("Tạo: ").AppendLine(created);
        }

        AppendMounts(sb, root);
        AppendNetworks(sb, root);

        return sb.Length == 0
            ? "Không có trường tóm tắt quen thuộc trong inspect."
            : sb.ToString().TrimEnd();
    }

    private static void AppendEnvBlock(StringBuilder sb, JsonElement config)
    {
        if (!config.TryGetProperty("Env", out JsonElement env) || env.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        int i = 0;
        foreach (JsonElement line in env.EnumerateArray())
        {
            if (line.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (i >= MaxEnvLines)
            {
                int rest = env.GetArrayLength() - MaxEnvLines;
                if (rest > 0)
                {
                    sb.Append("… và ").Append(rest).AppendLine(" biến môi trường khác.");
                }

                break;
            }

            sb.Append("Env: ").AppendLine(line.GetString() ?? string.Empty);
            i++;
        }
    }

    private static void AppendMounts(StringBuilder sb, JsonElement root)
    {
        if (!root.TryGetProperty("Mounts", out JsonElement mounts) || mounts.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        int n = 0;
        foreach (JsonElement m in mounts.EnumerateArray())
        {
            if (n >= 12)
            {
                sb.AppendLine("… (còn mount khác)");
                break;
            }

            string src = TryGetStringMember(m, "Source") ?? "?";
            string dst = TryGetStringMember(m, "Destination") ?? "?";
            string typ = TryGetStringMember(m, "Type") ?? "";
            sb.Append("Mount").Append(n + 1).Append(" [").Append(typ).Append("]: ").Append(src).Append(" → ").AppendLine(dst);
            n++;
        }
    }

    private static void AppendNetworks(StringBuilder sb, JsonElement root)
    {
        if (!root.TryGetProperty("NetworkSettings", out JsonElement ns))
        {
            return;
        }

        if (!ns.TryGetProperty("Networks", out JsonElement nets) || nets.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty p in nets.EnumerateObject())
        {
            string ip = "?";
            if (p.Value.ValueKind == JsonValueKind.Object
                && p.Value.TryGetProperty("IPAddress", out JsonElement ipEl)
                && ipEl.ValueKind == JsonValueKind.String)
            {
                ip = ipEl.GetString() ?? "?";
            }

            sb.Append("Mạng ").Append(p.Name).Append(": IP ").AppendLine(ip);
        }
    }

    private static bool TryGetString(JsonElement el, string name, out string? value)
    {
        value = null;
        if (!el.TryGetProperty(name, out JsonElement p) || p.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = p.GetString();
        return true;
    }

    private static string? TryGetStringMember(JsonElement el, string name)
    {
        return el.TryGetProperty(name, out JsonElement p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
    }
}
