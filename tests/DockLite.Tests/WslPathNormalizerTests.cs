using DockLite.Infrastructure.Wsl;

namespace DockLite.Tests;

public sealed class WslPathNormalizerTests
{
    [Fact]
    public void NormalizeForWslpathArgument_WslLocalhostUnc_với_dấu_xuôi_chuyển_thành_backslash_đầy_đủ()
    {
        const string input = "//wsl.localhost/Ubuntu-22.04/home/user/wsl-docker-service";
        string actual = WslPathNormalizer.NormalizeForWslpathArgument(input);
        Assert.Equal(@"\\wsl.localhost\Ubuntu-22.04\home\user\wsl-docker-service", actual);
    }

    [Fact]
    public void NormalizeForWslpathArgument_WslLocalhostUnc_giữ_nguyên_khi_da_dung_backslash()
    {
        const string input = @"\\wsl.localhost\Ubuntu-22.04\home\user\proj";
        string actual = WslPathNormalizer.NormalizeForWslpathArgument(input);
        Assert.Equal(input, actual);
    }

    [Fact]
    public void IsWslNetworkUncPath_nhan_dien_wsl_dollar()
    {
        Assert.True(WslPathNormalizer.IsWslNetworkUncPath(@"\\wsl$\Ubuntu\home"));
    }

    [Fact]
    public void TryUnixPathFromWslUnc_chuyen_doan_sau_distro_thanh_duong_tuyet_doi_trong_linux()
    {
        const string win = @"\\wsl.localhost\Ubuntu-22.04\home\user\workspace\projects\wsl-docker-service";
        bool ok = WslPathNormalizer.TryUnixPathFromWslUnc(win, "Ubuntu-22.04", out string unix, out string? hint);
        Assert.True(ok);
        Assert.Null(hint);
        Assert.Equal("/home/user/workspace/projects/wsl-docker-service", unix);
    }

    [Fact]
    public void TryUnixPathFromWslUnc_wsl_dollar_prefix()
    {
        const string win = @"\\wsl$\Ubuntu-22.04\home\user\proj";
        bool ok = WslPathNormalizer.TryUnixPathFromWslUnc(win, expectedDistro: null, out string unix, out _);
        Assert.True(ok);
        Assert.Equal("/home/user/proj", unix);
    }

    [Fact]
    public void TryUnixPathFromWslUnc_distro_khong_khop_thi_bao_loi()
    {
        const string win = @"\\wsl.localhost\Ubuntu-22.04\home\user\proj";
        bool ok = WslPathNormalizer.TryUnixPathFromWslUnc(win, "Debian", out _, out string? hint);
        Assert.False(ok);
        Assert.NotNull(hint);
        Assert.Contains("Ubuntu-22.04", hint, StringComparison.Ordinal);
        Assert.Contains("Debian", hint, StringComparison.Ordinal);
    }
}
