using System.Text.Json;
using DockLite.Contracts.Api;

namespace DockLite.Tests;

/// <summary>
/// Parse envelope JSON giống <see cref="DockLite.Infrastructure.Api.DockLiteApiClient"/>.
/// </summary>
public sealed class ApiEnvelopeJsonTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Deserialize_success_envelope_with_data()
    {
        const string json = """{"success":true,"data":{"items":[]}}""";
        ApiEnvelope<ContainerListData>? env = JsonSerializer.Deserialize<ApiEnvelope<ContainerListData>>(json, JsonOptions);
        Assert.NotNull(env);
        Assert.True(env.Success);
        Assert.NotNull(env.Data);
        Assert.Empty(env.Data.Items);
    }

    [Fact]
    public void Deserialize_error_envelope()
    {
        const string json = """{"success":false,"error":{"code":"TEST","message":"Lỗi thử"}}""";
        ApiEnvelope<ContainerListData>? env = JsonSerializer.Deserialize<ApiEnvelope<ContainerListData>>(json, JsonOptions);
        Assert.NotNull(env);
        Assert.False(env.Success);
        Assert.NotNull(env.Error);
        Assert.Equal("TEST", env.Error.Code);
        Assert.Equal("Lỗi thử", env.Error.Message);
    }

    [Fact]
    public void Deserialize_success_envelope_stats_batch()
    {
        const string json = """
            {"success":true,"data":{"items":[
              {"id":"abc","ok":true,"stats":{"readAt":"2026-01-01T00:00:00Z","cpuUsagePercent":1.5,"memoryUsageBytes":100,"memoryLimitBytes":200,"networkRxBytes":0,"networkTxBytes":0,"blockReadBytes":0,"blockWriteBytes":0}},
              {"id":"def","ok":false,"error":"no such container"}
            ]}}
            """;
        ApiEnvelope<ContainerStatsBatchData>? env = JsonSerializer.Deserialize<ApiEnvelope<ContainerStatsBatchData>>(json, JsonOptions);
        Assert.NotNull(env);
        Assert.True(env.Success);
        Assert.NotNull(env.Data);
        Assert.Equal(2, env.Data.Items.Count);
        Assert.True(env.Data.Items[0].Ok);
        Assert.NotNull(env.Data.Items[0].Stats);
        Assert.Equal(1.5, env.Data.Items[0].Stats!.CpuUsagePercent, 2);
        Assert.False(env.Data.Items[1].Ok);
        Assert.Equal("no such container", env.Data.Items[1].Error);
    }
}
