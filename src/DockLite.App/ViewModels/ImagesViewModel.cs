using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Models;
using DockLite.App.Services;
using DockLite.Contracts.Api;
using Microsoft.Win32;

namespace DockLite.App.ViewModels;

/// <summary>
/// Danh sách image: làm mới, tìm, inspect/history, pull, export/import tar, xóa, prune.
/// </summary>
public partial class ImagesViewModel : ObservableObject
{
    private const int ToastMessageMaxChars = 600;

    private readonly IImageScreenApi _imageApi;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly IAppShutdownToken _shutdownToken;
    private readonly AppShellActivityState _shellActivity;
    private List<ImageSummaryDto> _allItems = new();
    private readonly SearchDebounceHelper _searchDebounce;
    private readonly SemaphoreSlim _imageListRefreshGate = new(1, 1);

    public ImagesViewModel(
        IImageScreenApi imageApi,
        IDialogService dialogService,
        INotificationService notificationService,
        IAppShutdownToken shutdownToken,
        AppShellActivityState shellActivity)
    {
        _imageApi = imageApi;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _shutdownToken = shutdownToken;
        _shellActivity = shellActivity;
        _searchDebounce = new SearchDebounceHelper(ApplyFilter);
        UpdateBatchToolbarState();
    }

    /// <summary>Không có image sau khi tải xong.</summary>
    public bool ShowEmptyImageListHint => !IsBusy && _allItems.Count == 0;

    /// <summary>Có image nhưng ô tìm không khớp dòng nào.</summary>
    public bool ShowImageFilterEmptyHint => !IsBusy && _allItems.Count > 0 && FilteredItems.Count == 0;

    private void ReportNetworkException(Exception ex)
    {
        StatusMessage = NetworkErrorMessageMapper.FormatForUser(ex);
        _ = ApiErrorUiFeedback.ShowNetworkExceptionToastAsync(_notificationService, ex);
    }

    private void NotifyEmptyImageHints()
    {
        OnPropertyChanged(nameof(ShowEmptyImageListHint));
        OnPropertyChanged(nameof(ShowImageFilterEmptyHint));
    }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private SelectableImageRow? _selectedImage;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Còn dòng chưa tick trong danh sách đã lọc: Chọn tất cả.</summary>
    [ObservableProperty]
    private bool _canSelectAllFiltered;

    /// <summary>Có ít nhất một ô đã chọn: Bỏ chọn, Sao chép ID, Xóa đã chọn.</summary>
    [ObservableProperty]
    private bool _canClearSelection;

    /// <summary>Có ít nhất một dòng đã tick: sao chép ID image vào clipboard.</summary>
    [ObservableProperty]
    private bool _canCopySelectedIds;

    /// <summary>Có ít nhất một dòng đã tick: xóa hàng loạt.</summary>
    [ObservableProperty]
    private bool _canBatchRemove;

    /// <summary>
    /// Đang tải inspect/history/pull hoặc export/import (không dùng chung IsBusy của prune/xóa để tránh khóa toàn trang khi chỉ cần khóa khối chi tiết).
    /// </summary>
    [ObservableProperty]
    private bool _isDetailBusy;

    [ObservableProperty]
    private string _detailInspectText = string.Empty;

    [ObservableProperty]
    private string _pullReferenceText = string.Empty;

    [ObservableProperty]
    private string _pullLogText = string.Empty;

    public ObservableCollection<SelectableImageRow> FilteredItems { get; } = new();

    public ObservableCollection<ImageHistoryDisplayRow> HistoryRows { get; } = new();

    partial void OnSelectedImageChanged(SelectableImageRow? value)
    {
        DetailInspectText = string.Empty;
        PullLogText = string.Empty;
        HistoryRows.Clear();
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounce.Schedule();
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyEmptyImageHints();
        UpdateBatchToolbarState();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!_shellActivity.ShouldRefreshImageList)
        {
            return;
        }

        if (!await _imageListRefreshGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            try
            {
                await ReloadImageListCoreAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ReportNetworkException(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
        finally
        {
            _imageListRefreshGate.Release();
        }
    }

    /// <summary>
    /// Làm mới danh sách không bật <see cref="IsBusy"/> (dùng sau pull/import).
    /// </summary>
    private async Task ReloadImageListAsync()
    {
        if (!_shellActivity.ShouldRefreshImageList)
        {
            return;
        }

        if (!await _imageListRefreshGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            try
            {
                await ReloadImageListCoreAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ReportNetworkException(ex);
            }
        }
        finally
        {
            _imageListRefreshGate.Release();
        }
    }

    private async Task ReloadImageListCoreAsync()
    {
        ApiResult<ImageListData> res = await _imageApi.GetImagesAsync(_shutdownToken.Token).ConfigureAwait(true);
        if (!res.Success)
        {
            string err = res.Error?.Message
                ?? UiLanguageManager.TryLocalizeCurrent("Ui_Status_Common_ListLoadFailed", "Không đọc được danh sách.");
            StatusMessage = err;
            _ = ApiErrorUiFeedback.ShowWarningToastAsync(_notificationService, err);
            _allItems = new List<ImageSummaryDto>();
        }
        else
        {
            _allItems = res.Data?.Items ?? new List<ImageSummaryDto>();
            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Images_Status_LoadedImagesCountFormat",
                "Đã tải {0} image.",
                _allItems.Count);
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        foreach (SelectableImageRow row in FilteredItems)
        {
            row.PropertyChanged -= OnFilteredRowPropertyChanged;
        }

        string q = SearchText.Trim();
        IEnumerable<ImageSummaryDto> query = _allItems;
        if (q.Length > 0)
        {
            query = query.Where(i =>
                i.Repository.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Tag.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Id.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        FilteredItems.Clear();
        foreach (ImageSummaryDto i in query)
        {
            var row = new SelectableImageRow(i);
            row.PropertyChanged += OnFilteredRowPropertyChanged;
            FilteredItems.Add(row);
        }

        NotifyEmptyImageHints();
        UpdateBatchToolbarState();
    }

    private void OnFilteredRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableImageRow.IsSelected))
        {
            UpdateBatchToolbarState();
        }
    }

    /// <summary>
    /// Nút Chọn tất cả / Bỏ chọn / Sao chép ID / Xóa đã chọn — phụ thuộc ô tick.
    /// </summary>
    private void UpdateBatchToolbarState()
    {
        if (IsBusy)
        {
            CanSelectAllFiltered = false;
            CanClearSelection = false;
            CanCopySelectedIds = false;
            CanBatchRemove = false;
            return;
        }

        int n = FilteredItems.Count;
        int selectedCount = FilteredItems.Count(r => r.IsSelected);
        bool allSelected = n > 0 && selectedCount == n;
        bool anySelected = selectedCount > 0;

        CanSelectAllFiltered = n > 0 && !allSelected;
        CanClearSelection = anySelected;
        CanCopySelectedIds = anySelected;
        CanBatchRemove = anySelected;
    }

    [RelayCommand]
    private void SelectAllFiltered()
    {
        foreach (SelectableImageRow row in FilteredItems)
        {
            row.IsSelected = true;
        }

        UpdateBatchToolbarState();
    }

    [RelayCommand]
    private void ClearRowSelectionChecks()
    {
        foreach (SelectableImageRow row in FilteredItems)
        {
            row.IsSelected = false;
        }

        UpdateBatchToolbarState();
    }

    /// <summary>
    /// Sao chép ID image (đầy đủ) của các dòng đã tick, một ID mỗi dòng.
    /// </summary>
    [RelayCommand]
    private void CopySelectedIdsToClipboard()
    {
        List<string> ids = FilteredItems.Where(r => r.IsSelected).Select(r => r.Model.Id).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        Clipboard.SetText(string.Join(Environment.NewLine, ids));
        StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
            "Ui_Images_Status_CopyIdsDoneFormat",
            "Đã sao chép {0} ID image vào clipboard.",
            ids.Count);
    }

    [RelayCommand]
    private async Task BatchRemoveCheckedAsync()
    {
        List<SelectableImageRow> targets = FilteredItems.Where(r => r.IsSelected).ToList();
        if (targets.Count == 0)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Images_Status_SelectAtLeastOne",
                "Chọn ít nhất một image (ô chọn).");
            return;
        }

        if (!await _dialogService
                .ConfirmAsync(
                    $"Xóa {targets.Count} image đã chọn? (lần lượt theo ID)",
                    "Xác nhận",
                    DialogConfirmKind.Warning)
                .ConfigureAwait(true))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            foreach (SelectableImageRow t in targets)
            {
                var req = new ImageRemoveRequest { Id = t.Model.Id };
                ApiResult<EmptyApiPayload> res = await _imageApi.RemoveImageAsync(req, _shutdownToken.Token).ConfigureAwait(true);
                if (!res.Success)
                {
                    StatusMessage = res.Error?.Message
                        ?? UiLanguageManager.TryLocalizeCurrent("Ui_Images_Status_DeleteFailed", "Xóa thất bại.");
                    return;
                }
            }

            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Images_Status_DeletedCountFormat",
                "Đã xóa {0} image.",
                targets.Count);
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveSelectedAsync()
    {
        if (SelectedImage is null)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Images_Status_SelectOneToRemove",
                "Chọn một image để xóa.");
            return;
        }

        if (!await _dialogService
                .ConfirmAsync(
                    $"Xóa image {SelectedImage.Model.Repository}:{SelectedImage.Model.Tag} ({SelectedImage.Model.Id})?",
                    "Xác nhận",
                    DialogConfirmKind.Question)
                .ConfigureAwait(true))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var req = new ImageRemoveRequest { Id = SelectedImage.Model.Id };
            ApiResult<EmptyApiPayload> res = await _imageApi.RemoveImageAsync(req, _shutdownToken.Token).ConfigureAwait(true);
            if (!res.Success)
            {
                StatusMessage = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Images_Status_DeleteFailed", "Xóa thất bại.");
                return;
            }

            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Images_Status_RemovedOne", "Đã xóa image.");
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PruneDanglingAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var req = new ImagePruneRequest { AllUnused = false };
            ApiResult<ComposeCommandData> res = await _imageApi.PruneImagesAsync(req, _shutdownToken.Token).ConfigureAwait(true);
            await ApplyPruneResultAsync(res, "Prune image dangling").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex);
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PruneAllUnusedAsync()
    {
        if (!await _dialogService
                .ConfirmAsync(
                    "Xóa mọi image không được container nào sử dụng (docker image prune -a)? Thao tác không thể hoàn tác.",
                    "Xác nhận",
                    DialogConfirmKind.Warning)
                .ConfigureAwait(true))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var req = new ImagePruneRequest { AllUnused = true };
            ApiResult<ComposeCommandData> res = await _imageApi.PruneImagesAsync(req, _shutdownToken.Token).ConfigureAwait(true);
            await ApplyPruneResultAsync(res, "Prune image -a").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex);
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task ApplyPruneResultAsync(ApiResult<ComposeCommandData> res, string label)
    {
        if (!res.Success)
        {
            string msg = res.Error?.Message
                ?? UiLanguageManager.TryLocalizeCurrent("Ui_Status_Common_ErrorGeneric", "lỗi");
            if (!string.IsNullOrEmpty(res.Error?.Details))
            {
                msg += Environment.NewLine + res.Error.Details;
            }

            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Status_Common_LabelColonMessageFormat",
                "{0}: {1}",
                label,
                msg);
            await _notificationService
                .ShowAsync(
                    "DockLite — prune image",
                    TruncateForToast($"{label}: {msg}", ToastMessageMaxChars),
                    NotificationDisplayKind.Warning,
                    CancellationToken.None)
                .ConfigureAwait(true);
            return;
        }

        string output = res.Data?.Output ?? string.Empty;
        StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Images_Status_PruneResultOkSuffixFormat",
                "{0} thành công.",
                label)
            + (string.IsNullOrEmpty(output) ? string.Empty : Environment.NewLine + output);
        string toastBody = string.IsNullOrEmpty(output)
            ? UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Images_Status_PruneResultDoneFormat",
                "{0} hoàn tất.",
                label)
            : TruncateForToast(output, ToastMessageMaxChars);
        await _notificationService
            .ShowAsync(
                "DockLite — prune image",
                toastBody,
                NotificationDisplayKind.Success,
                CancellationToken.None)
            .ConfigureAwait(true);
    }

    private static string TruncateForToast(string message, int max)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        message = message.Trim();
        return message.Length <= max ? message : message.Substring(0, max) + "…";
    }

    [RelayCommand]
    private async Task LoadInspectAsync()
    {
        if (SelectedImage is null)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Images_Status_SelectForInspect",
                "Chọn một image để tải inspect.");
            return;
        }

        IsDetailBusy = true;
        try
        {
            ApiResult<ImageInspectData> res = await _imageApi
                .GetImageInspectAsync(SelectedImage.Model.Id, _shutdownToken.Token)
                .ConfigureAwait(true);
            if (!res.Success)
            {
                DetailInspectText = string.Empty;
                StatusMessage = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Images_Status_InspectLoadFailed", "Không đọc được inspect.");
                return;
            }

            string json = JsonSerializer.Serialize(
                res.Data!.Inspect,
                new JsonSerializerOptions { WriteIndented = true });
            DetailInspectText = json;
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Images_Status_InspectLoaded", "Đã tải inspect.");
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex);
        }
        finally
        {
            IsDetailBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        if (SelectedImage is null)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Images_Status_SelectForHistory",
                "Chọn một image để tải history.");
            return;
        }

        IsDetailBusy = true;
        HistoryRows.Clear();
        try
        {
            ApiResult<ImageHistoryData> res = await _imageApi
                .GetImageHistoryAsync(SelectedImage.Model.Id, _shutdownToken.Token)
                .ConfigureAwait(true);
            if (!res.Success)
            {
                StatusMessage = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Images_Status_HistoryLoadFailed", "Không đọc được history.");
                return;
            }

            foreach (ImageHistoryLayerDto layer in res.Data?.Items ?? new List<ImageHistoryLayerDto>())
            {
                string createdText = layer.Created > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(layer.Created).UtcDateTime.ToString(
                        "yyyy-MM-dd HH:mm 'UTC'",
                        CultureInfo.InvariantCulture)
                    : "";
                HistoryRows.Add(
                    new ImageHistoryDisplayRow
                    {
                        Id = layer.Id,
                        CreatedText = createdText,
                        CreatedBy = layer.CreatedBy,
                        SizeText = FormatBytes(layer.Size),
                        Comment = layer.Comment,
                    });
            }

            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Images_Status_HistoryLayersCountFormat",
                "Đã tải {0} layer history.",
                HistoryRows.Count);
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex);
        }
        finally
        {
            IsDetailBusy = false;
        }
    }

    [RelayCommand]
    private async Task PullImageAsync()
    {
        string reference = PullReferenceText.Trim();
        if (string.IsNullOrEmpty(reference))
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Images_Status_EnterPullReference",
                "Nhập reference image (ví dụ nginx:latest).");
            return;
        }

        IsDetailBusy = true;
        PullLogText = "Đang kéo image…";
        try
        {
            var req = new ImagePullRequest { Reference = reference };
            ApiResult<ImagePullResultData> res = await Task.Run(async () =>
                await _imageApi.PullImageAsync(req, _shutdownToken.Token).ConfigureAwait(false)).ConfigureAwait(true);
            if (!res.Success)
            {
                PullLogText = string.Empty;
                StatusMessage = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Images_Status_PullFailed", "Pull thất bại.");
                return;
            }

            PullLogText = res.Data?.Log ?? string.Empty;
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Images_Status_PullDone", "Pull hoàn tất (xem log trong khối chi tiết).");
            await ReloadImageListAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            PullLogText = string.Empty;
            ReportNetworkException(ex);
        }
        finally
        {
            IsDetailBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportImageTarAsync()
    {
        if (SelectedImage is null)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Images_Status_SelectForExport",
                "Chọn một image để xuất tar.");
            return;
        }

        string safeName = SanitizeFileName($"{SelectedImage.Model.Repository}_{SelectedImage.Model.Tag}".Replace(':', '_'));
        if (string.IsNullOrEmpty(safeName))
        {
            safeName = "image-export";
        }

        var dlg = new SaveFileDialog
        {
            Filter = "Tar (*.tar)|*.tar|Tất cả|*.*",
            DefaultExt = ".tar",
            FileName = safeName + ".tar",
        };
        if (dlg.ShowDialog() != true)
        {
            return;
        }

        string imageId = SelectedImage.Model.Id;
        string outPath = dlg.FileName;
        IsDetailBusy = true;
        try
        {
            (bool ok, string? err) = await Task.Run(async () =>
            {
                await using var fs = new FileStream(
                    outPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    options: FileOptions.Asynchronous);
                return await _imageApi.DownloadImageExportAsync(imageId, fs, _shutdownToken.Token).ConfigureAwait(false);
            }).ConfigureAwait(true);
            if (!ok)
            {
                StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                    "Ui_Images_Status_ExportFailedFormat",
                    "Xuất tar thất bại: {0}",
                    err ?? UiLanguageManager.TryLocalizeCurrent(
                        "Ui_Images_Status_ExportUnknownError",
                        "lỗi không xác định"));
                return;
            }

            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Images_Status_FileWrittenFormat",
                "Đã ghi file: {0}",
                outPath);
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex);
        }
        finally
        {
            IsDetailBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportImageTarAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Tar (*.tar)|*.tar|Tất cả|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog() != true)
        {
            return;
        }

        string inPath = dlg.FileName;
        IsDetailBusy = true;
        try
        {
            ApiResult<ImageLoadResultData> res = await Task.Run(async () =>
            {
                await using var fs = new FileStream(
                    inPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    options: FileOptions.Asynchronous);
                return await _imageApi.UploadImageLoadAsync(fs, _shutdownToken.Token).ConfigureAwait(false);
            }).ConfigureAwait(true);
            if (!res.Success)
            {
                StatusMessage = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Images_Status_ImportFailed", "Import thất bại.");
                return;
            }

            string msg = res.Data?.Message ?? string.Empty;
            StatusMessage = string.IsNullOrEmpty(msg)
                ? UiLanguageManager.TryLocalizeCurrent("Ui_Images_Status_ImportDone", "Import hoàn tất.")
                : UiLanguageManager.TryLocalizeFormatCurrent(
                    "Ui_Images_Status_ImportWithMessageFormat",
                    "Import: {0}",
                    msg);
            await ReloadImageListAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ReportNetworkException(ex);
        }
        finally
        {
            IsDetailBusy = false;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            bytes = 0;
        }

        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        int i = 0;
        while (v >= 1024 && i < units.Length - 1)
        {
            v /= 1024;
            i++;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", v, units[i]);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Trim();
    }
}
