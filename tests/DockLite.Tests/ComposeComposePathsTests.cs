using DockLite.Core.Compose;

namespace DockLite.Tests;

public sealed class ComposeComposePathsTests
{
    [Fact]
    public void ParseComposeFileLines_null_va_rong_tra_ve_rong()
    {
        Assert.Empty(ComposeComposePaths.ParseComposeFileLines(null));
        Assert.Empty(ComposeComposePaths.ParseComposeFileLines(""));
        Assert.Empty(ComposeComposePaths.ParseComposeFileLines("   \n  \r\n "));
    }

    [Fact]
    public void ParseComposeFileLines_tach_dong_trim_bo_trong()
    {
        List<string> a = ComposeComposePaths.ParseComposeFileLines("docker-compose.yml");
        Assert.Single(a);
        Assert.Equal("docker-compose.yml", a[0]);

        List<string> b = ComposeComposePaths.ParseComposeFileLines(" a.yml \r\n b.yml \n override.yml ");
        Assert.Equal(3, b.Count);
        Assert.Equal("a.yml", b[0]);
        Assert.Equal("b.yml", b[1]);
        Assert.Equal("override.yml", b[2]);
    }

    [Fact]
    public void FormatComposeFilesForEditor_null_hoac_rong_tra_ve_rong()
    {
        Assert.Equal(string.Empty, ComposeComposePaths.FormatComposeFilesForEditor(null));
        Assert.Equal(string.Empty, ComposeComposePaths.FormatComposeFilesForEditor(Array.Empty<string>()));
    }

    [Fact]
    public void FormatComposeFilesForEditor_join_theo_NewLine()
    {
        string s = ComposeComposePaths.FormatComposeFilesForEditor(new[] { "a.yml", "b.yml" });
        Assert.Equal("a.yml" + Environment.NewLine + "b.yml", s);
    }

    [Fact]
    public void BuildComposeFileArgsForDockerCli_rong_tra_ve_rong()
    {
        Assert.Equal(string.Empty, ComposeComposePaths.BuildComposeFileArgsForDockerCli(null));
        Assert.Equal(string.Empty, ComposeComposePaths.BuildComposeFileArgsForDockerCli(Array.Empty<string>()));
    }

    [Fact]
    public void BuildComposeFileArgsForDockerCli_mot_file_co_dau_nhay_don_trong_ten()
    {
        string args = ComposeComposePaths.BuildComposeFileArgsForDockerCli(new[] { "foo'bar.yml" });
        Assert.Equal(" -f " + ComposeComposePaths.BashSingleQuote("foo'bar.yml"), args);
    }

    [Fact]
    public void BuildComposeFileArgsForDockerCli_nhieu_f()
    {
        string args = ComposeComposePaths.BuildComposeFileArgsForDockerCli(new[] { "a.yml", "b.yml" });
        Assert.Equal(
            " -f " + ComposeComposePaths.BashSingleQuote("a.yml") + " -f " + ComposeComposePaths.BashSingleQuote("b.yml"),
            args);
    }

    [Fact]
    public void BashSingleQuote_rong()
    {
        Assert.Equal("''", ComposeComposePaths.BashSingleQuote(""));
    }
}
