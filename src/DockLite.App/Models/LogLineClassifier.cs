namespace DockLite.App.Models;

/// <summary>
/// Phân loại mức log theo từ khóa (không parse format cụ thể của từng ứng dụng).
/// </summary>
public static class LogLineClassifier
{
    public static LogSeverity Classify(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return LogSeverity.Normal;
        }

        string u = line.ToUpperInvariant();
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
