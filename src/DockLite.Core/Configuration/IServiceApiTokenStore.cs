namespace DockLite.Core.Configuration;

/// <summary>
/// Lưu mật khẩu API tới dịch vụ dịa phương (Windows: Credential Manager với tên tài khoản theo hồ sơ).
/// </summary>
public interface IServiceApiTokenStore
{
    /// <summary>
    /// Đọc mật khẩu cho hồ sơ (ví dụ <c>default</c>); trả về <see langword="null"/> nếu chưa lưu.
    /// </summary>
    string? Read(string profile);

    /// <summary>
    /// Ghi hoặc cập nhật mật khẩu. Truyền <see langword="null"/> hoặc rỗng để xoá bản ghi tương ứng.
    /// </summary>
    void Write(string profile, string? token);

    /// <summary>
    /// Xoá bản ghi hồ sơ (bỏ qua nếu không tồn tại).
    /// </summary>
    void Remove(string profile);
}
