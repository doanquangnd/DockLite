namespace DockLite.Infrastructure.Wsl;

/// <summary>
/// Chuẩn hóa đường dẫn Windows trước khi truyền cho <c>wsl.exe wslpath</c> hoặc dựng đường Unix từ UNC WSL.
/// </summary>
public static class WslPathNormalizer
{
    private const string PrefixWslLocalhost = @"\\wsl.localhost\";
    private const string PrefixWslDollar = @"\\wsl$\";

    /// <summary>
    /// UNC tới filesystem WSL (Explorer / mạng) — không dùng <see cref="Path.GetFullPath"/> vì .NET có thể làm hỏng dấu phân cách
    /// (kết quả kiểu <c>/mnt/c/wsl.localhostUbuntu-22.04home...</c> khi gọi wslpath).
    /// </summary>
    public static bool IsWslNetworkUncPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string n = path.Trim().Replace('/', '\\');
        return n.StartsWith(PrefixWslLocalhost, StringComparison.OrdinalIgnoreCase)
            || n.StartsWith(PrefixWslDollar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// UNC <c>\\wsl.localhost\Distro\home\...</c> hoặc <c>\\wsl$\Distro\...</c> trỏ vào filesystem Linux trong distro.
    /// Không dùng <c>wslpath</c>: trên nhiều máy nó trả <c>/mnt/c/wsl.localhost/...</c> — không tồn tại trong WSL;
    /// đúng là <c>/home/...</c> (phần sau segment tên distro).
    /// </summary>
    /// <param name="expectedDistro">Nếu có (từ cài đặt), phải khớp segment distro trong đường dẫn.</param>
    /// <param name="mismatchHint">Lý do từ chối khi distro không khớp.</param>
    public static bool TryUnixPathFromWslUnc(
        string normalizedWindowsPath,
        string? expectedDistro,
        out string unixPath,
        out string? mismatchHint)
    {
        unixPath = "";
        mismatchHint = null;
        string n = normalizedWindowsPath.Trim().TrimEnd('\\');
        if (!IsWslNetworkUncPath(n))
        {
            return false;
        }

        string rest;
        if (n.StartsWith(PrefixWslLocalhost, StringComparison.OrdinalIgnoreCase))
        {
            rest = n[PrefixWslLocalhost.Length..];
        }
        else if (n.StartsWith(PrefixWslDollar, StringComparison.OrdinalIgnoreCase))
        {
            rest = n[PrefixWslDollar.Length..];
        }
        else
        {
            return false;
        }

        // rest = "Ubuntu-22.04\home\user\...\wsl-docker-service"
        int firstSep = rest.IndexOf('\\');
        if (firstSep <= 0)
        {
            return false;
        }

        string distroFromPath = rest[..firstSep];
        string pathAfterDistro = rest[(firstSep + 1)..];
        if (string.IsNullOrEmpty(pathAfterDistro))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expectedDistro))
        {
            string exp = expectedDistro.Trim();
            if (!distroFromPath.Equals(exp, StringComparison.OrdinalIgnoreCase))
            {
                mismatchHint = $"Tên distro trong đường dẫn ({distroFromPath}) khác ô Distro WSL ({exp}).";
                return false;
            }
        }

        unixPath = "/" + pathAfterDistro.Replace('\\', '/').Trim('/');
        return true;
    }

    /// <summary>
    /// Chuẩn bị đường dẫn làm đối số cho <c>wslpath -a</c>: thống nhất <c>/</c> thành <c>\</c>;
    /// với UNC WSL chỉ chuẩn hóa, không gọi <see cref="Path.GetFullPath"/>.
    /// </summary>
    public static string NormalizeForWslpathArgument(string windowsPath)
    {
        if (string.IsNullOrWhiteSpace(windowsPath))
        {
            return windowsPath;
        }

        string trimmed = windowsPath.Trim();
        string withBackslashes = trimmed.Replace('/', '\\');

        if (IsWslNetworkUncPath(withBackslashes))
        {
            return withBackslashes;
        }

        return Path.GetFullPath(withBackslashes);
    }
}
