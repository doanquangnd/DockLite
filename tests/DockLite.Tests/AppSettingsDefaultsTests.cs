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

    [Theory]
    [InlineData(29, 120)]
    [InlineData(601, 120)]
    [InlineData(60, 60)]
    public void Normalize_clamp_http_timeout_seconds(int input, int expected)
    {
        var s = new AppSettings { HttpTimeoutSeconds = input };
        AppSettingsDefaults.Normalize(s);
        Assert.Equal(expected, s.HttpTimeoutSeconds);
    }
}
