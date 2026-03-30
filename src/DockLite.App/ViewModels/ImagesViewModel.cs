using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Models;
using DockLite.App.Services;
using DockLite.Contracts.Api;
using DockLite.Core;
using DockLite.Core.Services;

namespace DockLite.App.ViewModels;

/// <summary>
/// Danh sách image: làm mới, tìm, xóa image đã chọn, prune dangling / tất cả không dùng.
/// </summary>
public partial class ImagesViewModel : ObservableObject
{
    private const int ToastMessageMaxChars = 600;

    private readonly IDockLiteApiClient _apiClient;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly IAppShutdownToken _shutdownToken;
    private List<ImageSummaryDto> _allItems = new();

    public ImagesViewModel(
        IDockLiteApiClient apiClient,
        IDialogService dialogService,
        INotificationService notificationService,
        IAppShutdownToken shutdownToken)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _shutdownToken = shutdownToken;
    }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private SelectableImageRow? _selectedImage;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<SelectableImageRow> FilteredItems { get; } = new();

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            ApiResult<ImageListData> res = await _apiClient.GetImagesAsync(_shutdownToken.Token).ConfigureAwait(true);
            if (!res.Success)
            {
                StatusMessage = res.Error?.Message ?? "Không đọc được danh sách.";
                _allItems = new List<ImageSummaryDto>();
            }
            else
            {
                _allItems = res.Data?.Items ?? new List<ImageSummaryDto>();
                StatusMessage = $"Đã tải {_allItems.Count} image.";
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
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
            FilteredItems.Add(new SelectableImageRow(i));
        }
    }

    [RelayCommand]
    private void SelectAllFiltered()
    {
        foreach (SelectableImageRow row in FilteredItems)
        {
            row.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearRowSelectionChecks()
    {
        foreach (SelectableImageRow row in FilteredItems)
        {
            row.IsSelected = false;
        }
    }

    [RelayCommand]
    private async Task BatchRemoveCheckedAsync()
    {
        List<SelectableImageRow> targets = FilteredItems.Where(r => r.IsSelected).ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "Chọn ít nhất một image (ô chọn).";
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
                ApiResult<EmptyApiPayload> res = await _apiClient.RemoveImageAsync(req, _shutdownToken.Token).ConfigureAwait(true);
                if (!res.Success)
                {
                    StatusMessage = res.Error?.Message ?? "Xóa thất bại.";
                    return;
                }
            }

            StatusMessage = $"Đã xóa {targets.Count} image.";
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
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
            StatusMessage = "Chọn một image để xóa.";
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
            ApiResult<EmptyApiPayload> res = await _apiClient.RemoveImageAsync(req, _shutdownToken.Token).ConfigureAwait(true);
            if (!res.Success)
            {
                StatusMessage = res.Error?.Message ?? "Xóa thất bại.";
                return;
            }

            StatusMessage = "Đã xóa image.";
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
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
            ApiResult<ComposeCommandData> res = await _apiClient.PruneImagesAsync(req, _shutdownToken.Token).ConfigureAwait(true);
            await ApplyPruneResultAsync(res, "Prune image dangling").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
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
            ApiResult<ComposeCommandData> res = await _apiClient.PruneImagesAsync(req, _shutdownToken.Token).ConfigureAwait(true);
            await ApplyPruneResultAsync(res, "Prune image -a").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
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
            string msg = res.Error?.Message ?? "lỗi";
            if (!string.IsNullOrEmpty(res.Error?.Details))
            {
                msg += Environment.NewLine + res.Error.Details;
            }

            StatusMessage = $"{label}: {msg}";
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
        StatusMessage = $"{label} thành công." + (string.IsNullOrEmpty(output) ? string.Empty : Environment.NewLine + output);
        string toastBody = string.IsNullOrEmpty(output)
            ? $"{label} hoàn tất."
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
}
