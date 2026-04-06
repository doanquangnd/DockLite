using Microsoft.Win32;

namespace DockLite.App.Services;

/// <summary>
/// Đọc chế độ sáng/tối ưu tiên của Windows (ứng dụng) qua registry.
/// </summary>
public static class WindowsSystemTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>
    /// Trả về true nếu Windows đang dùng chủ đề tối cho ứng dụng (AppsUseLightTheme = 0).
    /// </summary>
    public static bool IsDarkModePreferred()
    {
        try
        {
            using RegistryKey? k = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: false);
            if (k is null)
            {
                return false;
            }

            object? v = k.GetValue("AppsUseLightTheme");
            if (v is int i)
            {
                return i == 0;
            }
        }
        catch
        {
            // Mặc định sáng nếu không đọc được registry.
        }

        return false;
    }
}
