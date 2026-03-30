using System.Linq;
using System.Text;

namespace DockLite.Core.Diagnostics;

/// <summary>
/// Ghi log ứng dụng ra file trong %LocalAppData%\DockLite\logs (hỗ trợ gỡ lỗi).
/// </summary>
public static class AppFileLog
{
    private static readonly object Sync = new();

    /// <summary>
    /// Thư mục chứa file docklite-yyyyMMdd.log.
    /// </summary>
    public static string LogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DockLite", "logs");

    /// <summary>
    /// Ghi nhiều dòng (mỗi dòng một bản ghi cùng category).
    /// </summary>
    public static void WriteMultiline(string category, string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        foreach (string line in message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            Write(category, line);
        }
    }

    /// <summary>
    /// Ghi một dòng log (UTC ISO 8601, tab phân tách).
    /// </summary>
    public static void Write(string category, string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                string file = Path.Combine(LogDirectory, $"docklite-{DateTime.UtcNow:yyyyMMdd}.log");
                string line = $"{DateTime.UtcNow:O}\t{category}\t{message.Replace('\r', ' ').Replace('\n', ' ')}{Environment.NewLine}";
                File.AppendAllText(file, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Không ném ra: log không được phá luồng chính.
        }
    }

    /// <summary>
    /// Ghi ngoại lệ đầy đủ (giữ nhiều dòng trong file).
    /// </summary>
    public static void WriteException(string category, Exception ex)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                string file = Path.Combine(LogDirectory, $"docklite-{DateTime.UtcNow:yyyyMMdd}.log");
                var sb = new StringBuilder();
                sb.Append(DateTime.UtcNow.ToString("O")).Append('\t').Append(category).Append('\t');
                sb.AppendLine(ex.ToString());
                File.AppendAllText(file, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // bỏ qua
        }
    }

    /// <summary>
    /// Đọc phần cuối file log mới nhất (tối đa maxChars ký tự).
    /// </summary>
    public static string ReadRecentTail(int maxChars = 200_000)
    {
        try
        {
            lock (Sync)
            {
                if (!Directory.Exists(LogDirectory))
                {
                    return "(Chưa có thư mục log.)";
                }

                string? latest = Directory.GetFiles(LogDirectory, "docklite-*.log")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (latest is null)
                {
                    return "(Chưa có file log.)";
                }

                string text = File.ReadAllText(latest, Encoding.UTF8);
                if (text.Length <= maxChars)
                {
                    return text;
                }

                return text.Substring(text.Length - maxChars);
            }
        }
        catch (Exception ex)
        {
            return "Không đọc được log: " + ex.Message;
        }
    }
}
