using DockLite.Contracts.Api;
using DockLite.Core.Services;

namespace DockLite.App.Services;

/// <summary>
/// Triển khai <see cref="IComposeScreenApi"/> bằng cách ủy quyền tới <see cref="IDockLiteApiClient"/>.
/// </summary>
public sealed class ComposeScreenApi : IComposeScreenApi
{
    private readonly IDockLiteApiClient _client;

    public ComposeScreenApi(IDockLiteApiClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public Task<ApiResult<ComposeProjectListData>> GetComposeProjectsAsync(CancellationToken cancellationToken = default) =>
        _client.GetComposeProjectsAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ComposeProjectAddData>> AddComposeProjectAsync(ComposeProjectAddRequest request, CancellationToken cancellationToken = default) =>
        _client.AddComposeProjectAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<EmptyApiPayload>> RemoveComposeProjectAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ProjectIdError(projectId);
        return err is not null
            ? Task.FromResult(ApiResult<EmptyApiPayload>.Fail(err))
            : _client.RemoveComposeProjectAsync(projectId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ComposeProjectPatchData>> PatchComposeProjectAsync(
        string projectId,
        ComposeProjectPatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ProjectIdError(projectId);
        return err is not null
            ? Task.FromResult(ApiResult<ComposeProjectPatchData>.Fail(err))
            : _client.PatchComposeProjectAsync(projectId, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ComposeCommandData>> ComposeUpAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ProjectIdError(projectId);
        return err is not null
            ? Task.FromResult(ApiResult<ComposeCommandData>.Fail(err))
            : _client.ComposeUpAsync(projectId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ComposeCommandData>> ComposeDownAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ProjectIdError(projectId);
        return err is not null
            ? Task.FromResult(ApiResult<ComposeCommandData>.Fail(err))
            : _client.ComposeDownAsync(projectId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ComposeCommandData>> ComposePsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ProjectIdError(projectId);
        return err is not null
            ? Task.FromResult(ApiResult<ComposeCommandData>.Fail(err))
            : _client.ComposePsAsync(projectId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ComposeServiceListData>> ListComposeServicesAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ProjectIdError(projectId);
        return err is not null
            ? Task.FromResult(ApiResult<ComposeServiceListData>.Fail(err))
            : _client.ListComposeServicesAsync(projectId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ComposeCommandData>> ComposeServiceStartAsync(ComposeServiceRequest request, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ComposeServiceRequestError(request);
        return err is not null
            ? Task.FromResult(ApiResult<ComposeCommandData>.Fail(err))
            : _client.ComposeServiceStartAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ComposeCommandData>> ComposeServiceStopAsync(ComposeServiceRequest request, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ComposeServiceRequestError(request);
        return err is not null
            ? Task.FromResult(ApiResult<ComposeCommandData>.Fail(err))
            : _client.ComposeServiceStopAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ComposeCommandData>> ComposeServiceLogsAsync(ComposeServiceLogsRequest request, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ComposeServiceLogsRequestError(request);
        return err is not null
            ? Task.FromResult(ApiResult<ComposeCommandData>.Fail(err))
            : _client.ComposeServiceLogsAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ComposeCommandData>> ComposeServiceExecAsync(ComposeServiceExecRequest request, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ComposeServiceExecRequestError(request);
        return err is not null
            ? Task.FromResult(ApiResult<ComposeCommandData>.Fail(err))
            : _client.ComposeServiceExecAsync(request, cancellationToken);
    }
}
