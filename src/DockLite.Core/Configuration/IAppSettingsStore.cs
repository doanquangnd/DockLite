namespace DockLite.Core.Configuration;

/// <summary>
/// Lưu trữ cấu hình ứng dụng (JSON trong thư mục LocalApplicationData).
/// </summary>
public interface IAppSettingsStore
{
    /// <summary>
    /// Đường dẫn tuyệt đối tới file <c>settings.json</c> (hiển thị và sao lưu).
    /// </summary>
    string SettingsFilePath { get; }

    AppSettings Load();

    void Save(AppSettings settings);

    /// <summary>
    /// Sao chép file cài đặt ra <paramref name="destinationPath"/>; nếu file nguồn chưa tồn tại thì ghi JSON từ <see cref="Load"/> (mặc định đã chuẩn hóa).
    /// </summary>
    void ExportToCopy(string destinationPath);
}
