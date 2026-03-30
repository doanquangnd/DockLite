namespace DockLite.Infrastructure.Wsl;

/// <summary>
/// Tìm thư mục gốc wsl-docker-service (có scripts/run-server.sh) từ thư mục chứa exe.
/// </summary>
public static class WslDockerServicePathResolver
{
    private const int MaxParentLevels = 10;

    /// <summary>
    /// Đi ngược từ startDirectory (thường là AppContext.BaseDirectory) để tìm thư mục wsl-docker-service.
    /// </summary>
    public static string? TryFindFrom(string startDirectory)
    {
        if (string.IsNullOrWhiteSpace(startDirectory))
        {
            return null;
        }

        try
        {
            string? dir = Path.GetFullPath(startDirectory);
            for (int i = 0; i < MaxParentLevels && !string.IsNullOrEmpty(dir); i++)
            {
                string candidate = Path.Combine(dir, "wsl-docker-service");
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "scripts", "run-server.sh")))
                {
                    return candidate;
                }

                dir = Directory.GetParent(dir)?.FullName;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
