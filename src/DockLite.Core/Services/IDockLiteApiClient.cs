using System;
using System.Collections.Generic;
using System.IO;
using DockLite.Contracts.Api;

namespace DockLite.Core.Services;

/// <summary>
/// Client gọi API service DockLite trong WSL (REST, envelope JSON thống nhất trừ /api/health).
/// </summary>
public interface IDockLiteApiClient
{
    /// <summary>
    /// Gọi GET /api/health để kiểm tra service và phiên bản (không dùng envelope).
    /// </summary>
    Task<HealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gọi GET /api/docker/info để kiểm tra Docker Engine trong WSL.
    /// </summary>
    Task<ApiResult<DockerInfoData>> GetDockerInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/wsl/host-resources — RAM, load và đĩa gốc phía tiến trình service (Linux trong WSL).
    /// </summary>
    Task<ApiResult<WslHostResourcesData>> GetWslHostResourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Danh sách container (tất cả trạng thái).
    /// </summary>
    Task<ApiResult<ContainerListData>> GetContainersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/containers/{id}/start
    /// </summary>
    Task<ApiResult<EmptyApiPayload>> StartContainerAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/containers/{id}/stop
    /// </summary>
    Task<ApiResult<EmptyApiPayload>> StopContainerAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/containers/{id}/restart
    /// </summary>
    Task<ApiResult<EmptyApiPayload>> RestartContainerAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// DELETE /api/containers/{id}
    /// </summary>
    Task<ApiResult<EmptyApiPayload>> RemoveContainerAsync(string containerId, bool force, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/containers/{id}/logs?tail=
    /// </summary>
    Task<ApiResult<ContainerLogsData>> GetContainerLogsAsync(string containerId, int tail, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/containers/{id}/inspect — JSON inspect từ Docker Engine.
    /// </summary>
    Task<ApiResult<ContainerInspectData>> GetContainerInspectAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/containers/{id}/stats — một snapshot CPU/RAM/mạng.
    /// </summary>
    Task<ApiResult<ContainerStatsSnapshotData>> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/containers/stats-batch — nhiều snapshot trong một yêu cầu (tối đa 32 id).
    /// </summary>
    Task<ApiResult<ContainerStatsBatchData>> GetContainerStatsBatchAsync(
        IReadOnlyList<string> containerIds,
        CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeProjectListData>> GetComposeProjectsAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeProjectAddData>> AddComposeProjectAsync(ComposeProjectAddRequest request, CancellationToken cancellationToken = default);

    Task<ApiResult<EmptyApiPayload>> RemoveComposeProjectAsync(string projectId, CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeProjectPatchData>> PatchComposeProjectAsync(
        string projectId,
        ComposeProjectPatchRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeCommandData>> ComposeUpAsync(
        string projectId,
        IReadOnlyList<string>? composeProfiles = null,
        CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeCommandData>> ComposeDownAsync(
        string projectId,
        IReadOnlyList<string>? composeProfiles = null,
        CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeCommandData>> ComposePsAsync(
        string projectId,
        IReadOnlyList<string>? composeProfiles = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/compose/config/validate — <c>docker compose config -q</c> (kiểm tra file hợp lệ, không in YAML gộp).
    /// </summary>
    Task<ApiResult<ComposeCommandData>> ComposeConfigValidateAsync(
        string projectId,
        IReadOnlyList<string>? composeProfiles = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/compose/config/services — tên service (docker compose config --services).
    /// </summary>
    Task<ApiResult<ComposeServiceListData>> ListComposeServicesAsync(
        string projectId,
        IReadOnlyList<string>? composeProfiles = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/compose/service/start
    /// </summary>
    Task<ApiResult<ComposeCommandData>> ComposeServiceStartAsync(ComposeServiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/compose/service/stop
    /// </summary>
    Task<ApiResult<ComposeCommandData>> ComposeServiceStopAsync(ComposeServiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/compose/service/logs
    /// </summary>
    Task<ApiResult<ComposeCommandData>> ComposeServiceLogsAsync(ComposeServiceLogsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/compose/service/exec — <c>docker compose exec -T</c> (lệnh không TTY).
    /// </summary>
    Task<ApiResult<ComposeCommandData>> ComposeServiceExecAsync(ComposeServiceExecRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/containers/top-by-memory?limit=5 — container đang chạy, sắp theo RAM.
    /// </summary>
    Task<ApiResult<ContainerTopMemoryData>> GetContainersTopByMemoryAsync(int limit = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/containers/top-by-cpu?limit=5 — container đang chạy, sắp theo CPU % (snapshot).
    /// </summary>
    Task<ApiResult<ContainerTopMemoryData>> GetContainersTopByCpuAsync(int limit = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/images
    /// </summary>
    Task<ApiResult<ImageListData>> GetImagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/images/remove
    /// </summary>
    Task<ApiResult<EmptyApiPayload>> RemoveImageAsync(ImageRemoveRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/images/prune
    /// </summary>
    Task<ApiResult<ComposeCommandData>> PruneImagesAsync(ImagePruneRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/system/prune
    /// </summary>
    Task<ApiResult<ComposeCommandData>> SystemPruneAsync(SystemPruneRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/images/{id}/inspect
    /// </summary>
    Task<ApiResult<ImageInspectData>> GetImageInspectAsync(string imageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/images/{id}/history
    /// </summary>
    Task<ApiResult<ImageHistoryData>> GetImageHistoryAsync(string imageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/images/pull
    /// </summary>
    Task<ApiResult<ImagePullResultData>> PullImageAsync(ImagePullRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/images/pull/stream — luồng thô từ daemon (không envelope JSON); cập nhật qua <paramref name="progress"/>.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> PullImageStreamAsync(
        ImagePullRequest request,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/images/load — thân application/x-tar (stream từ file).
    /// </summary>
    Task<ApiResult<ImageLoadResultData>> UploadImageLoadAsync(Stream tarStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/images/{id}/export — ghi luồng tar binary (không dùng envelope JSON).
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> DownloadImageExportAsync(
        string imageId,
        Stream destination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/networks
    /// </summary>
    Task<ApiResult<NetworkListData>> GetNetworksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/volumes
    /// </summary>
    Task<ApiResult<VolumeListData>> GetVolumesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/volumes/remove — xóa một volume theo tên.
    /// </summary>
    Task<ApiResult<EmptyApiPayload>> RemoveVolumeAsync(VolumeRemoveRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/docker/events/stream — NDJSON (mỗi dòng một sự kiện).
    /// </summary>
    Task StreamDockerEventsAsync(IProgress<string> lineProgress, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/images/trivy-scan — cần Trivy trong PATH trên WSL.
    /// </summary>
    Task<ApiResult<ImageTrivyScanResultData>> ScanImageTrivyAsync(ImageTrivyScanRequest request, CancellationToken cancellationToken = default);
}
