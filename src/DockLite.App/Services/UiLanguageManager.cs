using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using DockLite.Core.Configuration;

namespace DockLite.App.Services;

/// <summary>
/// Gộp từ điển chuỗi giao diện (vi/en) và đặt văn hóa UI thread theo <see cref="AppSettings.UiLanguage"/>.
/// </summary>
public static class UiLanguageManager
{
    private const string ViPack = "pack://application:,,,/DockLite.App;component/Resources/UiStrings.vi.xaml";
    private const string EnPack = "pack://application:,,,/DockLite.App;component/Resources/UiStrings.en.xaml";

    /// <summary>
    /// Áp dụng ngôn ngữ UI: <see cref="CultureInfo"/> và merged dictionary <c>UiStrings.*.xaml</c>.
    /// </summary>
    public static void Apply(Application app, AppSettings settings)
    {
        var probe = new AppSettings { UiLanguage = settings.UiLanguage };
        AppSettingsDefaults.Normalize(probe);
        string lang = probe.UiLanguage;

        var culture = lang == "en" ? new CultureInfo("en") : new CultureInfo("vi");
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;

        Collection<ResourceDictionary> merged = app.Resources.MergedDictionaries;
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            ResourceDictionary d = merged[i];
            if (d.Source != null && d.Source.ToString().Contains("UiStrings.", StringComparison.OrdinalIgnoreCase))
            {
                merged.RemoveAt(i);
            }
        }

        string pack = lang == "en" ? EnPack : ViPack;
        merged.Add(new ResourceDictionary { Source = new Uri(pack, UriKind.Absolute) });
    }

    /// <summary>
    /// Đọc chuỗi từ Application (sau khi đã gọi <see cref="Apply"/>); trả khóa nếu không tìm thấy.
    /// </summary>
    public static string FindString(Application app, string key)
    {
        if (app.TryFindResource(key) is string s && !string.IsNullOrEmpty(s))
        {
            return s;
        }

        return key;
    }

    /// <summary>
    /// Trả chuỗi theo khóa tài nguyên nếu có; nếu không có app hoặc không tìm thấy khóa thì trả <paramref name="fallback"/>.
    /// </summary>
    public static string TryLocalize(Application? app, string key, string fallback)
    {
        if (app is null)
        {
            return fallback;
        }

        string s = FindString(app, key);
        return s == key ? fallback : s;
    }

    /// <summary>
    /// Rút gọn: <see cref="Application.Current"/> (chuỗi trạng thái / thông báo ngắn).
    /// </summary>
    public static string TryLocalizeCurrent(string key, string fallbackVi)
    {
        return TryLocalize(Application.Current, key, fallbackVi);
    }

    /// <summary>
    /// Chuỗi định dạng theo <see cref="CultureInfo.CurrentUICulture"/>.
    /// </summary>
    public static string TryLocalizeFormat(Application? app, string key, string fallbackViFmt, params object[] args)
    {
        string fmt = TryLocalize(app, key, fallbackViFmt);
        return string.Format(CultureInfo.CurrentUICulture, fmt, args);
    }

    /// <summary>
    /// Định dạng với <see cref="Application.Current"/>.
    /// </summary>
    public static string TryLocalizeFormatCurrent(string key, string fallbackViFmt, params object[] args)
    {
        return TryLocalizeFormat(Application.Current, key, fallbackViFmt, args);
    }
}
