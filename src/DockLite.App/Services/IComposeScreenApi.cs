using DockLite.Contracts.Api;

namespace DockLite.App.Services;

/// <summary>
/// Lớp ứng dụng bọc <see cref="IDockLiteApiClient"/> cho màn Compose — tách gọi API khỏi ViewModel.
/// </summary>
public interface IComposeScreenApi
{
    Task<ApiResult<ComposeProjectListData>> GetComposeProjectsAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeProjectAddData>> AddComposeProjectAsync(ComposeProjectAddRequest request, CancellationToken cancellationToken = default);

    Task<ApiResult<EmptyApiPayload>> RemoveComposeProjectAsync(string projectId, CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeProjectPatchData>> PatchComposeProjectAsync(
        string projectId,
        ComposeProjectPatchRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeCommandData>> ComposeUpAsync(string projectId, CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeCommandData>> ComposeDownAsync(string projectId, CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeCommandData>> ComposePsAsync(string projectId, CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeServiceListData>> ListComposeServicesAsync(string projectId, CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeCommandData>> ComposeServiceStartAsync(ComposeServiceRequest request, CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeCommandData>> ComposeServiceStopAsync(ComposeServiceRequest request, CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeCommandData>> ComposeServiceLogsAsync(ComposeServiceLogsRequest request, CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeCommandData>> ComposeServiceExecAsync(ComposeServiceExecRequest request, CancellationToken cancellationToken = default);
}
