using DockLite.Core.Configuration;

namespace DockLite.Tests;

public sealed class AppSettingsDefaultsTests
{
    [Theory]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    [InlineData("vi", "vi")]
    [InlineData("", "vi")]
    [InlineData("  ", "vi")]
    [InlineData("fr", "vi")]
    public void Normalize_maps_ui_language_to_vi_or_en(string input, string expected)
    {
        var s = new AppSettings { UiLanguage = input };
        AppSettingsDefaults.Normalize(s);
        Assert.Equal(expected, s.UiLanguage);
    }
}
