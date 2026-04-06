using System.Collections.ObjectModel;
using System.Windows;
using DockLite.Core.Configuration;
using Microsoft.Win32;

namespace DockLite.App.Services;

/// <summary>
/// Gộp từ điển DarkTheme sau ModernTheme khi cài đặt yêu cầu (áp dụng đầy đủ nhất khi gọi trước khi tải cửa sổ chính).
/// </summary>
public static class ThemeManager
{
    private const string DarkThemePack = "pack://application:,,,/DockLite.App;component/Themes/DarkTheme.xaml";

    /// <summary>
    /// Áp dụng chủ đề theo <see cref="AppSettings.UiTheme"/> (Light; Dark; System — theo Windows).
    /// </summary>
    public static void Apply(Application app, AppSettings settings)
    {
        var probe = new AppSettings { UiTheme = settings.UiTheme };
        AppSettingsDefaults.Normalize(probe);
        string effective = ResolveEffectiveTheme(probe.UiTheme);

        Collection<ResourceDictionary> merged = app.Resources.MergedDictionaries;
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            ResourceDictionary d = merged[i];
            if (d.Source != null && d.Source.ToString().Contains("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase))
            {
                merged.RemoveAt(i);
            }
        }

        if (string.Equals(effective, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            merged.Add(new ResourceDictionary { Source = new Uri(DarkThemePack, UriKind.Absolute) });
        }
    }

    /// <summary>
    /// System → Light hoặc Dark theo Windows; Light/Dark giữ nguyên.
    /// </summary>
    public static string ResolveEffectiveTheme(string normalizedUiTheme)
    {
        if (string.Equals(normalizedUiTheme, "System", StringComparison.OrdinalIgnoreCase))
        {
            return WindowsSystemTheme.IsDarkModePreferred() ? "Dark" : "Light";
        }

        return normalizedUiTheme;
    }

    /// <summary>
    /// Khi <see cref="AppSettings.UiTheme"/> là System: lắng nghe đổi chủ đề Windows và áp dụng lại từ điển.
    /// </summary>
    public static void RegisterSystemThemeListener(Application app, IAppSettingsStore settingsStore)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(settingsStore);
        SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            try
            {
                AppSettings s = settingsStore.Load();
                AppSettingsDefaults.Normalize(s);
                if (!string.Equals(s.UiTheme, "System", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                app.Dispatcher.Invoke(() => Apply(app, s));
            }
            catch
            {
                // Bỏ qua khi đọc cài đặt hoặc UI không còn.
            }
        };
    }
}
