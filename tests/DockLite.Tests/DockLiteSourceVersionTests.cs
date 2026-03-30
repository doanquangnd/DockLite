using DockLite.Core;

namespace DockLite.Tests;

public sealed class DockLiteSourceVersionTests
{
    [Fact]
    public void TryParseVersionLine_accepts_three_part()
    {
        bool ok = DockLiteSourceVersion.TryParseVersionLine("0.1.0", out Version? v, out string? err);
        Assert.True(ok);
        Assert.NotNull(v);
        Assert.Null(err);
        Assert.Equal(0, v!.Major);
        Assert.Equal(1, v.Minor);
        Assert.Equal(0, v.Build);
    }

    [Fact]
    public void Version_compare_supports_ge_policy()
    {
        var a = new Version(0, 1, 1);
        var b = new Version(0, 1, 0);
        Assert.True(a.CompareTo(b) >= 0);
    }
}
