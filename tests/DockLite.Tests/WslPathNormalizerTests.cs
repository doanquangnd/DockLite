using DockLite.Infrastructure.Wsl;

namespace DockLite.Tests;

public sealed class WslPathNormalizerTests
{
    [Fact]
    public void NormalizeForWslpathArgument_drive_path_dung_dau_xuoi_tranh_loi_wslpath()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Thư mục C++ gây đường dẫn có nhiều dấu \; chuẩn hóa phải ra C:/... để wsl.exe không làm hỏng chuỗi.
        string input = @"C:\Users\test\work\C++\DockLite\wsl-docker-service";
        string n = WslPathNormalizer.NormalizeForWslpathArgument(input);
        Assert.StartsWith("C:/", n, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('\\', n);
    }
}
