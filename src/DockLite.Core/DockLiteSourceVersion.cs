namespace DockLite.Core;

/// <summary>
/// Đọc và so sánh phiên bản mã nguồn (file VERSION một dòng, ví dụ 0.1.0) dùng cho đồng bộ WSL.
/// </summary>
public static class DockLiteSourceVersion
{
    public const string VersionFileName = "VERSION";

    /// <summary>
    /// Đọc file VERSION trong thư mục gốc wsl-docker-service (clone repo): ưu tiên <c>internal/appversion/VERSION</c>, không có thì <c>VERSION</c> ở gốc (bản cũ).
    /// </summary>
    public static bool TryReadFromWindowsDirectory(string directory, out Version? version, out string? errorMessage)
    {
        version = null;
        errorMessage = null;
        // Go embed không cho phép ../ — file VERSION nằm cùng package internal/appversion.
        string nested = Path.Combine(directory, "internal", "appversion", VersionFileName);
        string path = File.Exists(nested)
            ? nested
            : Path.Combine(directory, VersionFileName);
        if (!File.Exists(path))
        {
            errorMessage =
                "Không tìm thấy file " + VersionFileName + " trong thư mục nguồn. Thêm một dòng phiên bản (ví dụ 0.1.0) hoặc tắt tùy chọn chỉ đồng bộ khi version nguồn >= đích.";
            return false;
        }

        string text = File.ReadAllText(path).Trim();
        return TryParseVersionLine(text, out version, out errorMessage);
    }

    /// <summary>
    /// Parse một dòng (Major.Minor.Build[.Revision]) — dùng chung với nội dung file VERSION trong WSL.
    /// </summary>
    public static bool TryParseVersionLine(string line, out Version? version, out string? errorMessage)
    {
        version = null;
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            errorMessage = "Chuỗi version trong file VERSION rỗng.";
            return false;
        }

        string t = line.Trim();
        try
        {
            version = Version.Parse(t);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = "Không chấp nhận được version \"" + t + "\": " + ex.Message;
            return false;
        }
    }
}
