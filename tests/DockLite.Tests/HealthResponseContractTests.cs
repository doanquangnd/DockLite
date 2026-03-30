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
    public void Deserialize_docker_info_ok_khop_contract()
    {
        const string json = """
            {"ok":true,"serverVersion":"24.0.0","operatingSystem":"Ubuntu","osType":"linux",
            "kernelVersion":"5.15","containers":3,"containersRunning":1,"images":5}
            """;
        var result = JsonSerializer.Deserialize<DockerInfoResponse>(json);
        Assert.NotNull(result);
        Assert.True(result.Ok);
        Assert.Equal("24.0.0", result.ServerVersion);
        Assert.Equal(1, result.ContainersRunning);
        Assert.Equal(3, result.Containers);
    }

    [Fact]
    public void Deserialize_container_list_khop_contract()
    {
        const string json = """{"items":[{"id":"ab","shortId":"ab","name":"n1","image":"img","status":"Up","ports":""}],"error":null}""";
        var result = JsonSerializer.Deserialize<ContainerListResponse>(json);
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal("n1", result.Items[0].Name);
    }

    [Fact]
    public void Deserialize_container_logs_khop_contract()
    {
        const string json = """{"content":"line1\nline2","error":null}""";
        var result = JsonSerializer.Deserialize<ContainerLogsResponse>(json);
        Assert.NotNull(result);
        Assert.Equal("line1\nline2", result.Content);
    }

    [Fact]
    public void Deserialize_compose_projects_khop_contract()
    {
        const string json = """{"ok":true,"items":[{"id":"a1","wslPath":"/mnt/c/p","name":"p"}]}""";
        var result = JsonSerializer.Deserialize<ComposeProjectListApiResponse>(json);
        Assert.NotNull(result);
        Assert.True(result.Ok);
        Assert.Single(result.Items!);
        Assert.Equal("/mnt/c/p", result.Items![0].WslPath);
    }
}
