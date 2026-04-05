using System.Collections.Generic;
using System.Text;
using DockLite.Core.Configuration;

namespace DockLite.Core.Diagnostics;

/// <summary>
/// Ghi sự kiện chẩn đoán cục bộ (opt-in), file tách khỏi log ứng dụng thường.
/// Không ghi nội dung nhạy cảm: không mật khẩu URL, không stack trace đầy đủ, không thân phản hồi API.
/// </summary>
public static class DiagnosticTelemetry
{
    private static readonly object Sync = new();

    private static int _enabled;

    /// <summary>
    /// Bật/tắt ghi file (đồng bộ với cài đặt đã lưu; gọi khi khởi tạo shell và sau Lưu).
    /// </summary>
    public static void SetEnabled(bool enabled)
    {
        Volatile.Write(ref _enabled, enabled ? 1 : 0);
    }

    /// <summary>
    /// Trạng thái hiện tại (volatile).
    /// </summary>
    public static bool IsEnabled => Volatile.Read(ref _enabled) != 0;

    /// <summary>
    /// Chuẩn hóa base URL chỉ còn scheme + host + cổng (không đường dẫn, không userinfo).
    /// </summary>
    public static string FormatBaseUrlForTelemetry(string? serviceBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(serviceBaseUrl))
        {
            return "";
        }

        try
        {
            var u = new Uri(serviceBaseUrl.Trim(), UriKind.Absolute);
            return u.Scheme + "://" + u.Host + ":" + u.Port;
        }
        catch
        {
            return "(uri_invalid)";
        }
    }

    /// <summary>
    /// Ghi một dòng sự kiện (UTC ISO 8601, tab, payload key=value an toàn).
    /// </summary>
    public static void WriteEvent(string eventId, params (string key, string value)[] data)
    {
        if (!IsEnabled || string.IsNullOrEmpty(eventId))
        {
            return;
        }

        try
        {
            var sb = new StringBuilder();
            sb.Append("event=").Append(SanitizeToken(eventId));
            foreach ((string key, string value) in data)
            {
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                sb.Append(' ').Append(SanitizeToken(key)).Append('=').Append(SanitizeToken(value));
            }

            WriteLine(sb.ToString());
        }
        catch
        {
            // Không làm gián đoạn luồng chính.
        }
    }

    /// <summary>
    /// Ghi khi hết số lần thử HTTP đọc (chỉ loại ngoại lệ, không nội dung chi tiết).
    /// </summary>
    public static void WriteHttpReadExhausted(string exceptionKind, int attempts)
    {
        WriteEvent(
            "http_read_exhausted",
            ("exception_kind", exceptionKind),
            ("attempts", attempts.ToString()));
    }

    /// <summary>
    /// Start / Stop / Restart / Build service WSL từ Cài đặt hoặc header (không ghi chuỗi thông báo từ API).
    /// </summary>
    /// <param name="healthOk">Chỉ có nghĩa với start/restart sau chờ health; ngược lại để null.</param>
    public static void WriteManualWslLifecycle(
        AppSettings settings,
        string source,
        string verb,
        bool commandSent,
        bool? healthOk = null)
    {
        if (!IsEnabled)
        {
            return;
        }

        var pairs = new List<(string key, string value)>
        {
            ("base_url", FormatBaseUrlForTelemetry(settings.ServiceBaseUrl)),
            ("source", source),
            ("verb", verb),
            ("command_sent", commandSent.ToString()),
        };
        if (healthOk.HasValue)
        {
            pairs.Add(("health_ok", healthOk.Value.ToString()));
        }

        WriteEvent("manual_wsl_lifecycle", pairs.ToArray());
    }

    /// <summary>
    /// Kết quả «Kiểm tra kết nối» trong Cài đặt (chỉ loại ngoại lệ khi lỗi, không message).
    /// </summary>
    public static void WriteTestConnection(AppSettings settings, bool success, string? failureExceptionTypeName)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (success)
        {
            WriteEvent(
                "test_connection",
                ("base_url", FormatBaseUrlForTelemetry(settings.ServiceBaseUrl)),
                ("result", "ok"));
        }
        else
        {
            WriteEvent(
                "test_connection",
                ("base_url", FormatBaseUrlForTelemetry(settings.ServiceBaseUrl)),
                ("result", "fail"),
                ("exception_type", string.IsNullOrEmpty(failureExceptionTypeName) ? "unknown" : failureExceptionTypeName));
        }
    }

    /// <summary>
    /// Đồng bộ mã Windows → WSL (nút trong Cài đặt); không ghi đường dẫn đầy đủ.
    /// </summary>
    public static void WriteSyncCodeToWsl(AppSettings settings, bool success)
    {
        if (!IsEnabled)
        {
            return;
        }

        WriteEvent(
            "sync_code_wsl",
            ("base_url", FormatBaseUrlForTelemetry(settings.ServiceBaseUrl)),
            ("ok", success.ToString()));
    }

    private static void WriteLine(string message)
    {
        lock (Sync)
        {
            Directory.CreateDirectory(AppFileLog.LogDirectory);
            string file = Path.Combine(
                AppFileLog.LogDirectory,
                $"docklite-diagnostic-{DateTime.UtcNow:yyyyMMdd}.log");
            string line = $"{DateTime.UtcNow:O}\t{message.Replace('\r', ' ').Replace('\n', ' ')}{Environment.NewLine}";
            File.AppendAllText(file, line, Encoding.UTF8);
        }
    }

    private static string SanitizeToken(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "-";
        }

        string s = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        if (s.Length > 256)
        {
            return s.Substring(0, 253) + "...";
        }

        return s;
    }
}
