using System.Text.Json;
using System.Text.Json.Nodes;
using DockLite.Core.Configuration;
using DockLite.Core.Diagnostics;

namespace DockLite.Infrastructure.Configuration;

/// <summary>
/// Triển khai lưu cài đặt vào %LocalAppData%\DockLite\settings.json; mật khẩu API tách sang <see cref="IServiceApiTokenStore"/>.
/// </summary>
public sealed class AppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions MigrationJsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly IServiceApiTokenStore _apiTokenStore;
    private readonly string _filePath;

    /// <summary>
    /// Tạo store với kho bí mật tích hợp (Windows: Credential Manager).
    /// </summary>
    public AppSettingsStore(IServiceApiTokenStore apiTokenStore)
    {
        _apiTokenStore = apiTokenStore;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DockLite");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");
    }

    /// <inheritdoc />
    public string SettingsFilePath => _filePath;

    /// <inheritdoc />
    public void ExportToCopy(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        string full = Path.GetFullPath(destinationPath);
        string? parent = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        if (File.Exists(_filePath))
        {
            File.Copy(_filePath, full, overwrite: true);
        }
        else
        {
            AppSettings snapshot = Load();
            string json = JsonSerializer.Serialize(snapshot, JsonOptions);
            File.WriteAllText(full, json);
        }
    }

    /// <inheritdoc />
    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            var empty = new AppSettings();
            AppSettingsDefaults.Normalize(empty);
            empty.ServiceApiToken = _apiTokenStore.Read(empty.ServiceApiTokenProfile);
            return empty;
        }

        try
        {
            TryMigratePlaintextServiceApiToken();

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
            loaded.ServiceApiToken = _apiTokenStore.Read(loaded.ServiceApiTokenProfile);
            return loaded;
        }
        catch
        {
            var fallback = new AppSettings();
            AppSettingsDefaults.Normalize(fallback);
            fallback.ServiceApiToken = _apiTokenStore.Read(fallback.ServiceApiTokenProfile);
            return fallback;
        }
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        AppSettingsDefaults.Normalize(settings);
        _apiTokenStore.Write(settings.ServiceApiTokenProfile, settings.ServiceApiToken);
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    private void TryMigratePlaintextServiceApiToken()
    {
        string text;
        try
        {
            text = File.ReadAllText(_filePath);
        }
        catch
        {
            return;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(text);
        }
        catch
        {
            return;
        }

        if (root is not JsonObject obj)
        {
            return;
        }

        // Giá trị cũ có thể là "ServiceApiToken" (Pascal) hoặc "serviceApiToken" (JSON camel).
        const string pascalKey = nameof(AppSettings.ServiceApiToken);
        const string camelKey = "serviceApiToken";
        JsonNode? tokenNode = null;
        if (obj.TryGetPropertyValue(pascalKey, out var n1))
        {
            tokenNode = n1;
        }
        else if (obj.TryGetPropertyValue(camelKey, out var n2))
        {
            tokenNode = n2;
        }

        if (tokenNode is null)
        {
            return;
        }

        string? tokenStr = null;
        if (tokenNode is JsonValue tVal && tVal.TryGetValue<string?>(out string? tStr))
        {
            tokenStr = tStr;
        }

        if (string.IsNullOrWhiteSpace(tokenStr))
        {
            obj.Remove(pascalKey);
            obj.Remove(camelKey);
            try
            {
                File.WriteAllText(_filePath, obj.ToJsonString(MigrationJsonOptions));
            }
            catch
            {
            }

            return;
        }

        string profile = "default";
        if (obj.TryGetPropertyValue(nameof(AppSettings.ServiceApiTokenProfile), out var profNode) &&
            profNode is JsonValue pVal &&
            pVal.TryGetValue<string?>(out string? p1) &&
            !string.IsNullOrWhiteSpace(p1))
        {
            profile = p1!.Trim();
        }
        else if (obj.TryGetPropertyValue("serviceApiTokenProfile", out var profCamel) &&
                 profCamel is JsonValue pVal2 &&
                 pVal2.TryGetValue<string?>(out string? p2) &&
                 !string.IsNullOrWhiteSpace(p2))
        {
            profile = p2!.Trim();
        }

        _apiTokenStore.Write(profile, tokenStr.Trim());
        obj.Remove(pascalKey);
        obj.Remove(camelKey);

        try
        {
            File.WriteAllText(_filePath, obj.ToJsonString(MigrationJsonOptions));
            AppFileLog.Write(
                "credential_migration",
                "Da chuyen ServiceApiToken tu settings.json sang Trinh quan ly thong tin xac thuc Windows (Credential Manager).");
        }
        catch
        {
        }
    }
}
