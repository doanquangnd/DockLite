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

    [Theory]
    [InlineData("System", "System")]
    [InlineData("SYSTEM", "System")]
    [InlineData("Light", "Light")]
    public void Normalize_maps_ui_theme_including_system(string input, string expected)
    {
        var s = new AppSettings { UiTheme = input };
        AppSettingsDefaults.Normalize(s);
        Assert.Equal(expected, s.UiTheme);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(-1, 0)]
    [InlineData(101, 0)]
    public void Normalize_clamps_container_stats_warn_percent(int input, int expected)
    {
        var s = new AppSettings
        {
            ContainerStatsCpuWarnPercent = input,
            ContainerStatsMemoryWarnPercent = input,
        };
        AppSettingsDefaults.Normalize(s);
        Assert.Equal(expected, s.ContainerStatsCpuWarnPercent);
        Assert.Equal(expected, s.ContainerStatsMemoryWarnPercent);
    }
}
