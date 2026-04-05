using System.Text.Json;
using DockLite.Contracts.Api;

namespace DockLite.Tests;

/// <summary>
/// Ánh xạ <see cref="ApiEnvelope{T}"/> → <see cref="ApiResult{T}"/> (dùng chung với client HTTP).
/// </summary>
public sealed class ApiResultEnvelopeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void ToApiResult_null_envelope_is_parse_error()
    {
        ApiEnvelope<DockerInfoData>? env = null;
        ApiResult<DockerInfoData> r = env.ToApiResult();
        Assert.False(r.Success);
        Assert.Equal(DockLiteErrorCodes.Parse, r.Error?.Code);
    }

    [Fact]
    public void ToApiResult_success_ok()
    {
        var env = new ApiEnvelope<DockerInfoData>
        {
            Success = true,
            Data = new DockerInfoData
            {
                ServerVersion = "24.0",
                OperatingSystem = "Linux",
                OsType = "linux",
                KernelVersion = "6.0",
                Containers = 1,
                ContainersRunning = 1,
                Images = 2,
            },
        };
        ApiResult<DockerInfoData> r = env.ToApiResult();
        Assert.True(r.Success);
        Assert.NotNull(r.Data);
        Assert.Equal("24.0", r.Data.ServerVersion);
    }

    [Fact]
    public void ToApiResult_error_branch()
    {
        var env = new ApiEnvelope<DockerInfoData>
        {
            Success = false,
            Error = new ApiErrorBody { Code = DockLiteErrorCodes.DockerUnavailable, Message = "engine down" },
        };
        ApiResult<DockerInfoData> r = env.ToApiResult();
        Assert.False(r.Success);
        Assert.Equal(DockLiteErrorCodes.DockerUnavailable, r.Error?.Code);
        Assert.Equal("engine down", r.Error?.Message);
    }

    [Fact]
    public void ToApiResult_error_without_body_uses_unknown()
    {
        var env = new ApiEnvelope<DockerInfoData> { Success = false, Error = null };
        ApiResult<DockerInfoData> r = env.ToApiResult();
        Assert.False(r.Success);
        Assert.Equal(DockLiteErrorCodes.Unknown, r.Error?.Code);
    }

    [Fact]
    public void Deserialize_then_ToApiResult_matches_manual_envelope_test()
    {
        const string json = """{"success":true,"data":{"serverVersion":"1.0","operatingSystem":"Linux","osType":"linux","kernelVersion":"k","containers":0,"containersRunning":0,"images":0}}""";
        ApiEnvelope<DockerInfoData>? env = JsonSerializer.Deserialize<ApiEnvelope<DockerInfoData>>(json, JsonOptions);
        ApiResult<DockerInfoData> r = env.ToApiResult();
        Assert.True(r.Success);
        Assert.Equal("1.0", r.Data?.ServerVersion);
    }
}
