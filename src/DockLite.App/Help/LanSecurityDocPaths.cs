using System.IO;

namespace DockLite.App.Help;

/// <summary>
/// Phân giải đường dẫn tới <c>docs/docklite-lan-security.md</c> khi chạy từ thư mục build.
/// </summary>
internal static class LanSecurityDocPaths
{
    private const string RelativeUnderDocs = "docklite-lan-security.md";

    /// <summary>
    /// Trả về đường dẫn tuyệt đối nếu tệp tồn tại; ngược lại null.
    /// </summary>
    public static string? TryResolve(string appBaseDirectory)
    {
        if (string.IsNullOrWhiteSpace(appBaseDirectory))
        {
            return null;
        }

        string[] relativeCandidates =
        {
            Path.Combine("..", "..", "..", "..", "..", "docs", RelativeUnderDocs),
            Path.Combine("..", "..", "..", "..", "docs", RelativeUnderDocs),
            Path.Combine("docs", RelativeUnderDocs),
        };

        foreach (string rel in relativeCandidates)
        {
            string full = Path.GetFullPath(Path.Combine(appBaseDirectory, rel));
            if (File.Exists(full))
            {
                return full;
            }
        }

        return null;
    }
}
