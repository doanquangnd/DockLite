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
}
