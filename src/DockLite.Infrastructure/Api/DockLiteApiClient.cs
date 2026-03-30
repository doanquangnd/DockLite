using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using DockLite.Contracts.Api;
using DockLite.Core.Services;

namespace DockLite.Infrastructure.Api;

/// <summary>
/// Triển khai HTTP client gọi API DockLite (WSL), đọc envelope JSON thống nhất.
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
        return await HttpReadRetry.ExecuteNullableAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync("api/health", cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content
                    .ReadFromJsonAsync<HealthResponse>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<DockerInfoData>> GetDockerInfoAsync(CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync("api/docker/info", cancellationToken).ConfigureAwait(false);
                return await ReadEnvelopeAsync<DockerInfoData>(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ContainerListData>> GetContainersAsync(CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync("api/containers", cancellationToken).ConfigureAwait(false);
                return await ReadEnvelopeAsync<ContainerListData>(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<EmptyApiPayload>> StartContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsync(
                $"api/containers/{Uri.EscapeDataString(containerId)}/start",
                null,
                cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<EmptyApiPayload>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<EmptyApiPayload>> StopContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsync(
                $"api/containers/{Uri.EscapeDataString(containerId)}/stop",
                null,
                cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<EmptyApiPayload>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<EmptyApiPayload>> RestartContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsync(
                $"api/containers/{Uri.EscapeDataString(containerId)}/restart",
                null,
                cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<EmptyApiPayload>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<EmptyApiPayload>> RemoveContainerAsync(string containerId, bool force, CancellationToken cancellationToken = default)
    {
        var query = force ? "?force=true" : "?force=false";
        using var response = await _session.Client.DeleteAsync(
                $"api/containers/{Uri.EscapeDataString(containerId)}{query}",
                cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<EmptyApiPayload>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ContainerLogsData>> GetContainerLogsAsync(string containerId, int tail, CancellationToken cancellationToken = default)
    {
        string url = $"api/containers/{Uri.EscapeDataString(containerId)}/logs?tail={tail}";
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync(url, cancellationToken).ConfigureAwait(false);
                return await ReadEnvelopeAsync<ContainerLogsData>(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ContainerInspectData>> GetContainerInspectAsync(string containerId, CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync(
                        $"api/containers/{Uri.EscapeDataString(containerId)}/inspect",
                        cancellationToken)
                    .ConfigureAwait(false);
                return await ReadEnvelopeAsync<ContainerInspectData>(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ContainerStatsSnapshotData>> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync(
                        $"api/containers/{Uri.EscapeDataString(containerId)}/stats",
                        cancellationToken)
                    .ConfigureAwait(false);
                return await ReadEnvelopeAsync<ContainerStatsSnapshotData>(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeProjectListData>> GetComposeProjectsAsync(CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync("api/compose/projects", cancellationToken).ConfigureAwait(false);
                return await ReadEnvelopeAsync<ComposeProjectListData>(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeProjectAddData>> AddComposeProjectAsync(ComposeProjectAddRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/compose/projects", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<ComposeProjectAddData>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<EmptyApiPayload>> RemoveComposeProjectAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.DeleteAsync(
                $"api/compose/projects/{Uri.EscapeDataString(projectId)}",
                cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<EmptyApiPayload>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeCommandData>> ComposeUpAsync(string projectId, CancellationToken cancellationToken = default)
    {
        return await PostComposeCommandAsync("api/compose/up", projectId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeCommandData>> ComposeDownAsync(string projectId, CancellationToken cancellationToken = default)
    {
        return await PostComposeCommandAsync("api/compose/down", projectId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeCommandData>> ComposePsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        return await PostComposeCommandAsync("api/compose/ps", projectId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeServiceListData>> ListComposeServicesAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var body = new ComposeIdRequest { Id = projectId };
        using var response = await _session.Client.PostAsJsonAsync("api/compose/config/services", body, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<ComposeServiceListData>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeCommandData>> ComposeServiceStartAsync(ComposeServiceRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/compose/service/start", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<ComposeCommandData>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeCommandData>> ComposeServiceStopAsync(ComposeServiceRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/compose/service/stop", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<ComposeCommandData>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeCommandData>> ComposeServiceLogsAsync(ComposeServiceLogsRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/compose/service/logs", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<ComposeCommandData>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeCommandData>> ComposeServiceExecAsync(ComposeServiceExecRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/compose/service/exec", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<ComposeCommandData>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ContainerTopMemoryData>> GetContainersTopByMemoryAsync(int limit = 5, CancellationToken cancellationToken = default)
    {
        string url = "api/containers/top-by-memory?limit=" + limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync(url, cancellationToken).ConfigureAwait(false);
                return await ReadEnvelopeAsync<ContainerTopMemoryData>(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ContainerTopMemoryData>> GetContainersTopByCpuAsync(int limit = 5, CancellationToken cancellationToken = default)
    {
        string url = "api/containers/top-by-cpu?limit=" + limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync(url, cancellationToken).ConfigureAwait(false);
                return await ReadEnvelopeAsync<ContainerTopMemoryData>(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ApiResult<ComposeCommandData>> PostComposeCommandAsync(string relativeUrl, string projectId, CancellationToken cancellationToken)
    {
        var body = new ComposeIdRequest { Id = projectId };
        using var response = await _session.Client.PostAsJsonAsync(relativeUrl, body, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<ComposeCommandData>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ImageListData>> GetImagesAsync(CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync("api/images", cancellationToken).ConfigureAwait(false);
                return await ReadEnvelopeAsync<ImageListData>(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<EmptyApiPayload>> RemoveImageAsync(ImageRemoveRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/images/remove", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<EmptyApiPayload>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeCommandData>> PruneImagesAsync(ImagePruneRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/images/prune", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<ComposeCommandData>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeCommandData>> SystemPruneAsync(SystemPruneRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/system/prune", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<ComposeCommandData>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Đọc thân JSON envelope; mọi HTTP status đều thử parse nếu có JSON.
    /// </summary>
    private static async Task<ApiResult<T>> ReadEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return ApiResult<T>.Fail(new ApiErrorBody
            {
                Code = DockLiteErrorCodes.Parse,
                Message = "Phản hồi trống.",
            });
        }

        ApiEnvelope<T>? env;
        try
        {
            env = JsonSerializer.Deserialize<ApiEnvelope<T>>(text, JsonOptions);
        }
        catch (JsonException ex)
        {
            return ApiResult<T>.Fail(new ApiErrorBody
            {
                Code = DockLiteErrorCodes.Parse,
                Message = ex.Message,
            });
        }

        if (env is null)
        {
            return ApiResult<T>.Fail(new ApiErrorBody
            {
                Code = DockLiteErrorCodes.Parse,
                Message = "Không parse được envelope.",
            });
        }

        if (!env.Success)
        {
            return ApiResult<T>.Fail(env.Error ?? new ApiErrorBody
            {
                Code = DockLiteErrorCodes.Unknown,
                Message = "Lỗi không xác định.",
            });
        }

        return ApiResult<T>.Ok(env.Data);
    }
}
