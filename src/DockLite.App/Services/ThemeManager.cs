using System.Collections.ObjectModel;
using System.Windows;
using DockLite.Core.Configuration;

namespace DockLite.App.Services;

/// <summary>
/// Gộp từ điển DarkTheme sau ModernTheme khi cài đặt yêu cầu (áp dụng đầy đủ nhất khi gọi trước khi tải cửa sổ chính).
/// </summary>
public static class ThemeManager
{
    private const string DarkThemePack = "pack://application:,,,/DockLite.App;component/Themes/DarkTheme.xaml";

    /// <summary>
    /// Áp dụng chủ đề theo <see cref="AppSettings.UiTheme"/> (Light: chỉ ModernTheme; Dark: thêm DarkTheme).
    /// </summary>
    public static void Apply(Application app, AppSettings settings)
    {
        var probe = new AppSettings { UiTheme = settings.UiTheme };
        AppSettingsDefaults.Normalize(probe);
        string theme = probe.UiTheme;

        Collection<ResourceDictionary> merged = app.Resources.MergedDictionaries;
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            ResourceDictionary d = merged[i];
            if (d.Source != null && d.Source.ToString().Contains("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase))
            {
                merged.RemoveAt(i);
            }
        }

        if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            merged.Add(new ResourceDictionary { Source = new Uri(DarkThemePack, UriKind.Absolute) });
        }
    }
}
