using System.Text;

namespace DockLite.Core.Compose;

/// <summary>
/// Chuẩn hóa danh sách file compose (mỗi dòng một đường dẫn tương đối) và đối số <c>-f</c> cho lệnh <c>docker compose</c> trên CLI.
/// </summary>
public static class ComposeComposePaths
{
    /// <summary>
    /// Tách nhiều dòng thành danh sách đường dẫn đã trim, bỏ dòng trống.
    /// </summary>
    public static List<string> ParseComposeFileLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        var lines = new List<string>();
        foreach (string line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string t = line.Trim();
            if (t.Length > 0)
            {
                lines.Add(t);
            }
        }

        return lines;
    }

    /// <summary>
    /// Ghép các đường dẫn thành một chuỗi nhiều dòng cho ô chỉnh sửa (CRLF trên Windows).
    /// </summary>
    public static string FormatComposeFilesForEditor(IReadOnlyList<string>? files)
    {
        if (files is null || files.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, files);
    }

    /// <summary>
    /// Tạo phần đối số sau <c>docker compose</c>, ví dụ <c> -f 'a.yml' -f 'b.yml'</c> (bash single-quote).
    /// </summary>
    public static string BuildComposeFileArgsForDockerCli(IReadOnlyList<string>? files)
    {
        if (files is null || files.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (string f in files)
        {
            string t = f?.Trim() ?? string.Empty;
            if (t.Length > 0)
            {
                sb.Append(" -f ").Append(BashSingleQuote(t));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Trích dẫn đơn bash cho đường dẫn file trong lệnh gợi ý WSL.
    /// </summary>
    public static string BashSingleQuote(string s)
    {
        return "'" + s.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }
}
