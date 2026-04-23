namespace DockLite.Core.Configuration;

/// <summary>
/// Mức độ cảnh báo khi Base URL trỏ ra ngoài loopback (Cài đặt).
/// </summary>
public enum ServiceBaseUrlSecuritySeverity
{
    None,
    Warning,
    Critical
}

/// <summary>
/// Phân tích URL dịch vụ để hiển thị cảnh báo phù hợp (HTTP cleartext trên LAN so với HTTPS trên LAN).
/// </summary>
public static class ServiceBaseUrlSecurityAnalyzer
{
    /// <summary>
    /// Trả về mức độ và thông điệp tiếng Việt (chuỗi rỗng nếu không cần cảnh báo).
    /// </summary>
    public static (ServiceBaseUrlSecuritySeverity Severity, string Message) Analyze(string? serviceBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(serviceBaseUrl))
        {
            return (ServiceBaseUrlSecuritySeverity.None, string.Empty);
        }

        if (!Uri.TryCreate(serviceBaseUrl.Trim(), UriKind.Absolute, out Uri? u))
        {
            return (ServiceBaseUrlSecuritySeverity.None, string.Empty);
        }

        if (IsLoopbackHost(u))
        {
            return (ServiceBaseUrlSecuritySeverity.None, string.Empty);
        }

        string scheme = u.Scheme.ToLowerInvariant();
        if (scheme == "http" || scheme == "ws")
        {
            return (
                ServiceBaseUrlSecuritySeverity.Critical,
                "Host không phải loopback: HTTP/WebSocket không mã hóa trên mạng LAN — có thể bị nghe lén (MITM). Chỉ dùng khi bạn tin cậy mạng hoặc có biện pháp bảo vệ tương đương.");
        }

        if (scheme == "https" || scheme == "wss")
        {
            return (
                ServiceBaseUrlSecuritySeverity.Warning,
                "Host không phải loopback: kết nối đã mã hóa (TLS) nhưng API vẫn có thể bị truy cập từ mạng LAN — kiểm soát tường lửa và token API.");
        }

        return (
            ServiceBaseUrlSecuritySeverity.Critical,
            "Host không phải loopback: kiểm tra scheme và rủi ro mạng trước khi dùng.");
    }

    private static bool IsLoopbackHost(Uri u)
    {
        string host = u.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host == "127.0.0.1"
            || host == "::1"
            || host.StartsWith("[::1]", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
