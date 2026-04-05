using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using DockLite.App.Services;

namespace DockLite.Tests;

/// <summary>
/// Map lỗi mạng sang chuỗi hiển thị (khi không có Application.Current, dùng chuỗi fallback trong code).
/// </summary>
public sealed class NetworkErrorMessageMapperTests
{
    [Fact]
    public void HttpRequestException_401_chua_token()
    {
        string msg = NetworkErrorMessageMapper.FormatForUser(
            new HttpRequestException("x", null, HttpStatusCode.Unauthorized));
        Assert.Contains("401", msg, StringComparison.Ordinal);
        Assert.Contains("token", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HttpRequestException_khong_status_dung_goi_y_service_wsl()
    {
        string msg = NetworkErrorMessageMapper.FormatForUser(new HttpRequestException("refused"));
        Assert.Contains("WSL", msg, StringComparison.Ordinal);
    }

    [Fact]
    public void SocketException_tra_ve_goi_y_ket_noi()
    {
        string msg = NetworkErrorMessageMapper.FormatForUser(new SocketException(10061));
        Assert.Contains("WSL", msg, StringComparison.Ordinal);
    }

    [Fact]
    public void TaskCanceledException_khong_huy_la_timeout()
    {
        using var cts = new CancellationTokenSource();
        var ex = new TaskCanceledException("request", new TimeoutException(), cts.Token);
        string msg = NetworkErrorMessageMapper.FormatForUser(ex);
        Assert.Contains("timeout", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AggregateException_mot_inner_duoc_goi_de_quy()
    {
        var inner = new HttpRequestException("x", null, HttpStatusCode.NotFound);
        var agg = new AggregateException(inner);
        string msg = NetworkErrorMessageMapper.FormatForUser(agg);
        Assert.Contains("404", msg, StringComparison.Ordinal);
    }
}
