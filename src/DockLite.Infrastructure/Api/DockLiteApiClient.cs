using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using DockLite.Contracts.Api;
using DockLite.Core.Services;

namespace DockLite.Infrastructure.Api;

/// <summary>
/// Triển khai HTTP client gọi API DockLite (WSL).
/// </summary>
public sealed class DockLiteApiClient : IDockLiteApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly DockLiteHttpSession _session;

    public DockLiteApiClient(DockLiteHttpSession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    public async Task<HealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync("api/health", cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DockerInfoResponse?> GetDockerInfoAsync(CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync("api/docker/info", cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<DockerInfoResponse>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ContainerListResponse?> GetContainersAsync(CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync("api/containers", cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ContainerListResponse>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiActionResponse?> StartContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsync(
            $"api/containers/{Uri.EscapeDataString(containerId)}/start",
            null,
            cancellationToken).ConfigureAwait(false);
        return await ReadActionAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiActionResponse?> StopContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsync(
            $"api/containers/{Uri.EscapeDataString(containerId)}/stop",
            null,
            cancellationToken).ConfigureAwait(false);
        return await ReadActionAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiActionResponse?> RestartContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsync(
            $"api/containers/{Uri.EscapeDataString(containerId)}/restart",
            null,
            cancellationToken).ConfigureAwait(false);
        return await ReadActionAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiActionResponse?> RemoveContainerAsync(string containerId, bool force, CancellationToken cancellationToken = default)
    {
        var query = force ? "?force=true" : "?force=false";
        using var response = await _session.Client.DeleteAsync(
            $"api/containers/{Uri.EscapeDataString(containerId)}{query}",
            cancellationToken).ConfigureAwait(false);
        return await ReadActionAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ContainerLogsResponse?> GetContainerLogsAsync(string containerId, int tail, CancellationToken cancellationToken = default)
    {
        string url = $"api/containers/{Uri.EscapeDataString(containerId)}/logs?tail={tail}";
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync(url, cancellationToken).ConfigureAwait(false);
                return await response.Content.ReadFromJsonAsync<ContainerLogsResponse>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ComposeProjectListApiResponse?> GetComposeProjectsAsync(CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync("api/compose/projects", cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ComposeProjectListApiResponse>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ComposeProjectAddApiResponse?> AddComposeProjectAsync(ComposeProjectAddRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/compose/projects", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ComposeProjectAddApiResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiActionResponse?> RemoveComposeProjectAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.DeleteAsync(
            $"api/compose/projects/{Uri.EscapeDataString(projectId)}",
            cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ApiActionResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ComposeCommandResponse?> ComposeUpAsync(string projectId, CancellationToken cancellationToken = default)
    {
        return await PostComposeCommandAsync("api/compose/up", projectId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ComposeCommandResponse?> ComposeDownAsync(string projectId, CancellationToken cancellationToken = default)
    {
        return await PostComposeCommandAsync("api/compose/down", projectId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ComposeCommandResponse?> ComposePsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        return await PostComposeCommandAsync("api/compose/ps", projectId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ComposeCommandResponse?> PostComposeCommandAsync(string relativeUrl, string projectId, CancellationToken cancellationToken)
    {
        var body = new ComposeIdRequest { Id = projectId };
        using var response = await _session.Client.PostAsJsonAsync(relativeUrl, body, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ComposeCommandResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ImageListResponse?> GetImagesAsync(CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync("api/images", cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ImageListResponse>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiActionResponse?> RemoveImageAsync(ImageRemoveRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/images/remove", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ApiActionResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ComposeCommandResponse?> PruneImagesAsync(ImagePruneRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/images/prune", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ComposeCommandResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ComposeCommandResponse?> SystemPruneAsync(SystemPruneRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/system/prune", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ComposeCommandResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ApiActionResponse?> ReadActionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<ApiActionResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }
}
