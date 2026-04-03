namespace DockLite.App.Models;

/// <summary>
/// Phân loại mức log theo từ khóa (không parse format cụ thể của từng ứng dụng).
/// </summary>
public static class LogLineClassifier
{
    /// <summary>
    /// Chỉ quét tiền tố dòng để tránh ToUpperInvariant/Contains trên chuỗi cực dài (một dòng JSON/Base64 hàng MB).
    /// </summary>
    private const int MaxClassifyScanChars = 4096;

    public static LogSeverity Classify(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return LogSeverity.Normal;
        }

        string scan = line.Length <= MaxClassifyScanChars ? line : line.Substring(0, MaxClassifyScanChars);
        string u = scan.ToUpperInvariant();
        if (u.Contains("ERROR") || u.Contains("FATAL"))
        {
            return LogSeverity.Error;
        }

        if (u.Contains("WARN"))
        {
            return LogSeverity.Warn;
        }

        if (u.Contains("DEBUG"))
        {
            return LogSeverity.Debug;
        }

        if (u.Contains("INFO"))
        {
            return LogSeverity.Info;
        }

        return LogSeverity.Normal;
    }
}
