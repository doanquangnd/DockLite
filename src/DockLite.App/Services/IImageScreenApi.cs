using System;
using System.IO;
using DockLite.Contracts.Api;

namespace DockLite.App.Services;

/// <summary>
/// Lớp ứng dụng bọc <see cref="IDockLiteApiClient"/> cho màn Image — tách gọi API khỏi ViewModel.
/// </summary>
public interface IImageScreenApi
{
    Task<ApiResult<ImageListData>> GetImagesAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<EmptyApiPayload>> RemoveImageAsync(ImageRemoveRequest request, CancellationToken cancellationToken = default);

    Task<ApiResult<ComposeCommandData>> PruneImagesAsync(ImagePruneRequest request, CancellationToken cancellationToken = default);

    Task<ApiResult<ImageInspectData>> GetImageInspectAsync(string imageId, CancellationToken cancellationToken = default);

    Task<ApiResult<ImageHistoryData>> GetImageHistoryAsync(string imageId, CancellationToken cancellationToken = default);

    Task<ApiResult<ImagePullResultData>> PullImageAsync(ImagePullRequest request, CancellationToken cancellationToken = default);

    Task<(bool Success, string? ErrorMessage)> PullImageStreamAsync(
        ImagePullRequest request,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default);

    Task<ApiResult<ImageLoadResultData>> UploadImageLoadAsync(Stream tarStream, CancellationToken cancellationToken = default);

    Task<(bool Success, string? ErrorMessage)> DownloadImageExportAsync(
        string imageId,
        Stream destination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/images/trivy-scan — cần Trivy trong PATH trên WSL.
    /// </summary>
    Task<ApiResult<ImageTrivyScanResultData>> ScanImageTrivyAsync(ImageTrivyScanRequest request, CancellationToken cancellationToken = default);
}
