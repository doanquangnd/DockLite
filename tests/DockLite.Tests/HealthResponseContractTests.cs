using System.Text.Json;
using DockLite.Contracts.Api;

namespace DockLite.Tests;

public sealed class HealthResponseContractTests
{
    [Fact]
    public void Deserialize_health_json_khop_contract()
    {
        const string json = """{"status":"ok","service":"docklite-wsl","version":"0.1.0"}""";
        var result = JsonSerializer.Deserialize<HealthResponse>(json);
        Assert.NotNull(result);
        Assert.Equal("ok", result.Status);
        Assert.Equal("docklite-wsl", result.Service);
        Assert.Equal("0.1.0", result.Version);
    }

    [Fact]
    public void Deserialize_envelope_docker_info_data_khop_contract()
    {
        const string json = """
            {"success":true,"data":{"serverVersion":"24.0.0","operatingSystem":"Ubuntu","osType":"linux",
            "kernelVersion":"5.15","containers":3,"containersRunning":1,"images":5}}
            """;
        var env = JsonSerializer.Deserialize<ApiEnvelope<DockerInfoData>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(env);
        Assert.True(env.Success);
        Assert.NotNull(env.Data);
        Assert.Equal("24.0.0", env.Data.ServerVersion);
        Assert.Equal(1, env.Data.ContainersRunning);
        Assert.Equal(3, env.Data.Containers);
    }

    [Fact]
    public void Deserialize_envelope_container_list_khop_contract()
    {
        const string json = """
            {"success":true,"data":{"items":[{"id":"ab","shortId":"ab","name":"n1","image":"img","status":"Up","ports":""}]}}
            """;
        var env = JsonSerializer.Deserialize<ApiEnvelope<ContainerListData>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(env);
        Assert.True(env.Success);
        Assert.Single(env.Data!.Items);
        Assert.Equal("n1", env.Data.Items[0].Name);
    }

    [Fact]
    public void Deserialize_envelope_container_logs_khop_contract()
    {
        const string json = """{"success":true,"data":{"content":"line1\nline2"}}""";
        var env = JsonSerializer.Deserialize<ApiEnvelope<ContainerLogsData>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(env);
        Assert.True(env.Success);
        Assert.Equal("line1\nline2", env.Data!.Content);
    }

    [Fact]
    public void Deserialize_envelope_compose_projects_khop_contract()
    {
        const string json = """{"success":true,"data":{"items":[{"id":"a1","wslPath":"/mnt/c/p","name":"p"}]}}""";
        var env = JsonSerializer.Deserialize<ApiEnvelope<ComposeProjectListData>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(env);
        Assert.True(env.Success);
        Assert.Single(env.Data!.Items!);
        Assert.Equal("/mnt/c/p", env.Data.Items![0].WslPath);
    }
}
