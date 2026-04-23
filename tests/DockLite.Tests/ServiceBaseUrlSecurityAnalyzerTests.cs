using DockLite.Core.Configuration;
using Xunit;

namespace DockLite.Tests;

public class ServiceBaseUrlSecurityAnalyzerTests
{
    [Theory]
    [InlineData("http://127.0.0.1:17890/", ServiceBaseUrlSecuritySeverity.None)]
    [InlineData("http://192.168.0.1:17890/", ServiceBaseUrlSecuritySeverity.Critical)]
    [InlineData("https://10.0.0.1:17890/", ServiceBaseUrlSecuritySeverity.Warning)]
    [InlineData("http://[::1]:17890/", ServiceBaseUrlSecuritySeverity.None)]
    public void Analyze_maps_expected_severity(string url, ServiceBaseUrlSecuritySeverity expected)
    {
        var (sev, _) = ServiceBaseUrlSecurityAnalyzer.Analyze(url);
        Assert.Equal(expected, sev);
    }

    [Fact]
    public void Analyze_critical_has_message()
    {
        var (_, msg) = ServiceBaseUrlSecurityAnalyzer.Analyze("http://192.168.1.1/");
        Assert.False(string.IsNullOrWhiteSpace(msg));
    }
}
