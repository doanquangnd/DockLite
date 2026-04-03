using System.Text.Json;
using DockLite.App.Models;

namespace DockLite.App.Services;

/// <summary>
/// Trích các bảng từ JSON inspect thô (Engine) để hiển thị DataGrid; không ném ngoại lệ ra ngoài.
/// </summary>
public static class ContainerInspectGridParser
{
    /// <summary>
    /// Điền các collection đã được gọi Clear() trước đó.
    /// </summary>
    public static void Fill(
        JsonElement inspect,
        ICollection<InspectMountRow> mounts,
        ICollection<InspectPortRow> ports,
        ICollection<InspectEnvRow> env,
        ICollection<InspectLabelRow> labels,
        ICollection<InspectNetworkRow> networks)
    {
        try
        {
            FillCore(inspect, mounts, ports, env, labels, networks);
        }
        catch
        {
            // Bỏ qua: UI vẫn có JSON thô và tóm tắt văn bản.
        }
    }

    private static void FillCore(
        JsonElement root,
        ICollection<InspectMountRow> mounts,
        ICollection<InspectPortRow> ports,
        ICollection<InspectEnvRow> env,
        ICollection<InspectLabelRow> labels,
        ICollection<InspectNetworkRow> networks)
    {
        if (root.TryGetProperty("Mounts", out JsonElement mountsEl) && mountsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement m in mountsEl.EnumerateArray())
            {
                mounts.Add(
                    new InspectMountRow
                    {
                        Type = GetStringProp(m, "Type"),
                        Source = GetStringProp(m, "Source"),
                        Destination = GetStringProp(m, "Destination"),
                        Mode = GetStringProp(m, "Mode"),
                    });
            }
        }

        if (root.TryGetProperty("NetworkSettings", out JsonElement ns))
        {
            if (ns.TryGetProperty("Ports", out JsonElement portsEl) && portsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty p in portsEl.EnumerateObject())
                {
                    string containerPort = p.Name;
                    if (p.Value.ValueKind == JsonValueKind.Null)
                    {
                        ports.Add(
                            new InspectPortRow
                            {
                                ContainerPort = containerPort,
                                HostIp = "-",
                                HostPort = "(expose, chưa map host)",
                            });
                        continue;
                    }

                    if (p.Value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    bool any = false;
                    foreach (JsonElement binding in p.Value.EnumerateArray())
                    {
                        any = true;
                        ports.Add(
                            new InspectPortRow
                            {
                                ContainerPort = containerPort,
                                HostIp = GetStringProp(binding, "HostIp"),
                                HostPort = GetStringProp(binding, "HostPort"),
                            });
                    }

                    if (!any)
                    {
                        ports.Add(
                            new InspectPortRow
                            {
                                ContainerPort = containerPort,
                                HostIp = "-",
                                HostPort = "-",
                            });
                    }
                }
            }

            if (ns.TryGetProperty("Networks", out JsonElement netsEl) && netsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty net in netsEl.EnumerateObject())
                {
                    JsonElement v = net.Value;
                    networks.Add(
                        new InspectNetworkRow
                        {
                            Name = net.Name,
                            IpAddress = v.ValueKind == JsonValueKind.Object
                                ? GetStringProp(v, "IPAddress")
                                : string.Empty,
                            Gateway = v.ValueKind == JsonValueKind.Object
                                ? GetStringProp(v, "Gateway")
                                : string.Empty,
                            MacAddress = v.ValueKind == JsonValueKind.Object
                                ? GetStringProp(v, "MacAddress")
                                : string.Empty,
                        });
                }
            }
        }

        if (root.TryGetProperty("Config", out JsonElement config))
        {
            if (config.TryGetProperty("Env", out JsonElement envEl) && envEl.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement line in envEl.EnumerateArray())
                {
                    if (line.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string s = line.GetString() ?? string.Empty;
                    SplitEnv(s, out string k, out string val);
                    env.Add(new InspectEnvRow { Key = k, Value = val });
                }
            }

            if (config.TryGetProperty("Labels", out JsonElement labEl) && labEl.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty lp in labEl.EnumerateObject())
                {
                    string val = lp.Value.ValueKind == JsonValueKind.String
                        ? (lp.Value.GetString() ?? string.Empty)
                        : lp.Value.ToString();
                    labels.Add(new InspectLabelRow { Key = lp.Name, Value = val });
                }
            }
        }
    }

    private static void SplitEnv(string line, out string key, out string value)
    {
        int i = line.IndexOf('=');
        if (i <= 0)
        {
            key = line;
            value = string.Empty;
            return;
        }

        key = line[..i];
        value = line[(i + 1)..];
    }

    private static string GetStringProp(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out JsonElement p))
        {
            return string.Empty;
        }

        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString() ?? string.Empty,
            JsonValueKind.Number => p.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty,
        };
    }
}
