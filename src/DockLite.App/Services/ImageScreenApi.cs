using System;
using System.IO;
using DockLite.Contracts.Api;
using DockLite.Core.Services;

namespace DockLite.App.Services;

/// <summary>
/// Triển khai <see cref="IImageScreenApi"/> bằng cách ủy quyền tới <see cref="IDockLiteApiClient"/>.
/// </summary>
public sealed class ImageScreenApi : IImageScreenApi
{
    private readonly IDockLiteApiClient _client;

    public ImageScreenApi(IDockLiteApiClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public Task<ApiResult<ImageListData>> GetImagesAsync(CancellationToken cancellationToken = default) =>
        _client.GetImagesAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<EmptyApiPayload>> RemoveImageAsync(ImageRemoveRequest request, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ImageRemoveRequestError(request);
        return err is not null
            ? Task.FromResult(ApiResult<EmptyApiPayload>.Fail(err))
            : _client.RemoveImageAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ComposeCommandData>> PruneImagesAsync(ImagePruneRequest request, CancellationToken cancellationToken = default) =>
        _client.PruneImagesAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ImageInspectData>> GetImageInspectAsync(string imageId, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ImageIdError(imageId);
        return err is not null
            ? Task.FromResult(ApiResult<ImageInspectData>.Fail(err))
            : _client.GetImageInspectAsync(imageId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ImageHistoryData>> GetImageHistoryAsync(string imageId, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ImageIdError(imageId);
        return err is not null
            ? Task.FromResult(ApiResult<ImageHistoryData>.Fail(err))
            : _client.GetImageHistoryAsync(imageId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ImagePullResultData>> PullImageAsync(ImagePullRequest request, CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ImagePullRequestError(request);
        return err is not null
            ? Task.FromResult(ApiResult<ImagePullResultData>.Fail(err))
            : _client.PullImageAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<(bool Success, string? ErrorMessage)> PullImageStreamAsync(
        ImagePullRequest request,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ImagePullRequestError(request);
        return err is not null
            ? Task.FromResult<(bool, string?)>((false, err.Message))
            : _client.PullImageStreamAsync(request, progress, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ImageLoadResultData>> UploadImageLoadAsync(Stream tarStream, CancellationToken cancellationToken = default) =>
        _client.UploadImageLoadAsync(tarStream, cancellationToken);

    /// <inheritdoc />
    public Task<(bool Success, string? ErrorMessage)> DownloadImageExportAsync(
        string imageId,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ApiErrorBody? err = ScreenApiInputValidation.ImageIdError(imageId);
        if (err is not null)
        {
            return Task.FromResult<(bool Success, string? ErrorMessage)>((false, err.Message));
        }

        return _client.DownloadImageExportAsync(imageId, destination, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ImageTrivyScanResultData>> ScanImageTrivyAsync(ImageTrivyScanRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ImageRef))
        {
            return Task.FromResult(
                ApiResult<ImageTrivyScanResultData>.Fail(
                    new ApiErrorBody { Code = "validation", Message = "Thiếu image reference." }));
        }

        return _client.ScanImageTrivyAsync(request, cancellationToken);
    }
}
