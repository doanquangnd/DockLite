using System;
using DockLite.Infrastructure.Wsl;
using Xunit;

namespace DockLite.Tests;

public sealed class WslDockerServiceAutoStartTests
{
    [Theory]
    [InlineData("/tmp/'; rm -rf /", "'")]
    [InlineData("/tmp/$(rm -rf /)", "$")]
    [InlineData("/tmp/path\nnew", "\\n")]
    public void ValidateWslUnixPathForSpawn_PathInject_ThrowsArgumentException(string value, string expectedToken)
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => WslDockerServiceAutoStart.ValidateWslUnixPathForSpawn(value));
        Assert.Contains(expectedToken, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateWslUnixPathForSpawn_NormalPath_DoesNotThrow()
    {
        WslDockerServiceAutoStart.ValidateWslUnixPathForSpawn("/home/user/wsl-docker-service");
    }
}
