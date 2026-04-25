using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DockLite.Contracts.Api;
using DockLite.Core.Services;

namespace DockLite.Infrastructure.Api;

/// <summary>
/// Triển khai HTTP client gọi API DockLite (WSL), đọc envelope JSON thống nhất.
/// Mọi lệnh gọi dùng <c>using</c> trên <see cref="HttpResponseMessage"/>; thân phản hồi đọc qua
/// <see cref="HttpContent.ReadAsStringAsync(System.Threading.CancellationToken)"/> trong <see cref="ReadEnvelopeAsync{T}"/> (buffer một lần, không giữ stream mở).
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
    public async Task<ApiResult<AuthRotateData>> RotateServiceApiTokenAsync(
        AuthRotateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client
            .PostAsJsonAsync("api/auth/rotate", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<AuthRotateData>(response, cancellationToken).ConfigureAwait(false);
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
    public async Task<ApiResult<WslHostResourcesData>> GetWslHostResourcesAsync(CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync("api/wsl/host-resources", cancellationToken).ConfigureAwait(false);
                return await ReadEnvelopeAsync<WslHostResourcesData>(response, cancellationToken).ConfigureAwait(false);
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
    public async Task<ApiResult<ContainerStatsBatchData>> GetContainerStatsBatchAsync(
        IReadOnlyList<string> containerIds,
        CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                var body = new ContainerStatsBatchRequest { Ids = new List<string>(containerIds) };
                using var response = await _session.Client.PostAsJsonAsync(
                        "api/containers/stats-batch",
                        body,
                        JsonOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                return await ReadEnvelopeAsync<ContainerStatsBatchData>(response, cancellationToken).ConfigureAwait(false);
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
    public async Task<ApiResult<ComposeProjectPatchData>> PatchComposeProjectAsync(
        string projectId,
        ComposeProjectPatchRequest request,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Patch,
            $"api/compose/projects/{Uri.EscapeDataString(projectId)}")
        {
            Content = JsonContent.Create(request, mediaType: null, options: JsonOptions),
        };
        using var response = await _session.Client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        return await ReadEnvelopeAsync<ComposeProjectPatchData>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeCommandData>> ComposeUpAsync(
        string projectId,
        IReadOnlyList<string>? composeProfiles = null,
        CancellationToken cancellationToken = default)
    {
        return await PostComposeCommandAsync("api/compose/up", projectId, composeProfiles, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeCommandData>> ComposeDownAsync(
        string projectId,
        IReadOnlyList<string>? composeProfiles = null,
        CancellationToken cancellationToken = default)
    {
        return await PostComposeCommandAsync("api/compose/down", projectId, composeProfiles, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeCommandData>> ComposePsAsync(
        string projectId,
        IReadOnlyList<string>? composeProfiles = null,
        CancellationToken cancellationToken = default)
    {
        return await PostComposeCommandAsync("api/compose/ps", projectId, composeProfiles, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeCommandData>> ComposeConfigValidateAsync(
        string projectId,
        IReadOnlyList<string>? composeProfiles = null,
        CancellationToken cancellationToken = default)
    {
        return await PostComposeCommandAsync("api/compose/config/validate", projectId, composeProfiles, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ComposeServiceListData>> ListComposeServicesAsync(
        string projectId,
        IReadOnlyList<string>? composeProfiles = null,
        CancellationToken cancellationToken = default)
    {
        var body = new ComposeIdRequest { Id = projectId, Profiles = composeProfiles };
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

    private async Task<ApiResult<ComposeCommandData>> PostComposeCommandAsync(
        string relativeUrl,
        string projectId,
        IReadOnlyList<string>? composeProfiles,
        CancellationToken cancellationToken)
    {
        var body = new ComposeIdRequest { Id = projectId, Profiles = composeProfiles };
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

    /// <inheritdoc />
    public async Task<ApiResult<ImageInspectData>> GetImageInspectAsync(string imageId, CancellationToken cancellationToken = default)
    {
        string url = $"api/images/{Uri.EscapeDataString(imageId)}/inspect";
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync(url, cancellationToken).ConfigureAwait(false);
                return await ReadEnvelopeAsync<ImageInspectData>(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ImageHistoryData>> GetImageHistoryAsync(string imageId, CancellationToken cancellationToken = default)
    {
        string url = $"api/images/{Uri.EscapeDataString(imageId)}/history";
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync(url, cancellationToken).ConfigureAwait(false);
                return await ReadEnvelopeAsync<ImageHistoryData>(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ImagePullResultData>> PullImageAsync(ImagePullRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/images/pull", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<ImagePullResultData>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> PullImageStreamAsync(
        ImagePullRequest request,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        using var httpReq = new HttpRequestMessage(HttpMethod.Post, "api/images/pull/stream")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        using HttpResponseMessage response = await _session.Client
            .SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string err = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return (false, string.IsNullOrWhiteSpace(err) ? $"HTTP {(int)response.StatusCode}" : err);
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        var sb = new StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            sb.Append(buffer, 0, read);
            const int maxLen = 512 * 1024;
            if (sb.Length > maxLen)
            {
                sb.Remove(0, sb.Length - maxLen);
            }

            progress?.Report(sb.ToString());
        }

        return (true, null);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ImageLoadResultData>> UploadImageLoadAsync(Stream tarStream, CancellationToken cancellationToken = default)
    {
        using var content = new StreamContent(tarStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-tar");
        using var response = await _session.Client.PostAsync("api/images/load", content, cancellationToken).ConfigureAwait(false);
        return await ReadEnvelopeAsync<ImageLoadResultData>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> DownloadImageExportAsync(
        string imageId,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        string url = $"api/images/{Uri.EscapeDataString(imageId)}/export";
        using var response = await _session.Client
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string err = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return (false, string.IsNullOrWhiteSpace(err) ? $"HTTP {(int)response.StatusCode}" : err);
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await stream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        return (true, null);
    }

    /// <inheritdoc />
    public async Task<ApiResult<NetworkListData>> GetNetworksAsync(CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync("api/networks", cancellationToken).ConfigureAwait(false);
                return await ReadEnvelopeAsync<NetworkListData>(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<VolumeListData>> GetVolumesAsync(CancellationToken cancellationToken = default)
    {
        return await HttpReadRetry.ExecuteAsync(
            async () =>
            {
                using var response = await _session.Client.GetAsync("api/volumes", cancellationToken).ConfigureAwait(false);
                return await ReadEnvelopeAsync<VolumeListData>(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiResult<EmptyApiPayload>> RemoveVolumeAsync(VolumeRemoveRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/volumes/remove", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<EmptyApiPayload>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StreamDockerEventsAsync(IProgress<string> lineProgress, CancellationToken cancellationToken = default)
    {
        using var httpReq = new HttpRequestMessage(HttpMethod.Get, "api/docker/events/stream");
        using HttpResponseMessage response = await _session.Client
            .SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            lineProgress.Report(line);
        }
    }

    /// <inheritdoc />
    public async Task<ApiResult<ImageTrivyScanResultData>> ScanImageTrivyAsync(ImageTrivyScanRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _session.Client.PostAsJsonAsync("api/images/trivy-scan", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadEnvelopeAsync<ImageTrivyScanResultData>(response, cancellationToken).ConfigureAwait(false);
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

        return env.ToApiResult();
    }
}
