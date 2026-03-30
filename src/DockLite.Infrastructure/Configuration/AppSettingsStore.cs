using System.Text.Json;
using DockLite.Core.Configuration;

namespace DockLite.Infrastructure.Configuration;

/// <summary>
/// Triển khai lưu cấu hình vào %LocalAppData%\DockLite\settings.json.
/// </summary>
public sealed class AppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    public AppSettingsStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DockLite");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");
    }

    /// <inheritdoc />
    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            var empty = new AppSettings();
            AppSettingsDefaults.Normalize(empty);
            return empty;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            // File cũ không có khóa: giữ hành vi mặc định là bật tự khởi động WSL.
            using (var doc = JsonDocument.Parse(json))
            {
                if (!doc.RootElement.TryGetProperty(nameof(AppSettings.AutoStartWslService), out _))
                {
                    loaded.AutoStartWslService = true;
                }
            }

            AppSettingsDefaults.Normalize(loaded);
            return loaded;
        }
        catch
        {
            var fallback = new AppSettings();
            AppSettingsDefaults.Normalize(fallback);
            return fallback;
        }
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
