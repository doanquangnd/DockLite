namespace DockLite.Core.Configuration;

/// <summary>
/// Lưu trữ cấu hình ứng dụng (JSON trong thư mục LocalApplicationData).
/// </summary>
public interface IAppSettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
