using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.Contracts.Api;
using DockLite.Core;
using DockLite.Core.Services;

namespace DockLite.App.ViewModels;

/// <summary>
/// Danh sách image: làm mới, tìm, xóa image đã chọn, prune dangling / tất cả không dùng.
/// </summary>
public partial class ImagesViewModel : ObservableObject
{
    private readonly IDockLiteApiClient _apiClient;
    private List<ImageSummaryDto> _allItems = new();

    public ImagesViewModel(IDockLiteApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ImageSummaryDto? _selectedImage;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<ImageSummaryDto> FilteredItems { get; } = new();

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
            ImageListResponse? res = await _apiClient.GetImagesAsync().ConfigureAwait(true);
            _allItems = res?.Items ?? new List<ImageSummaryDto>();
            if (!string.IsNullOrWhiteSpace(res?.Error))
            {
                StatusMessage = res.Error!;
            }
            else
            {
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
            FilteredItems.Add(i);
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

        MessageBoxResult confirm = MessageBox.Show(
            $"Xóa image {SelectedImage.Repository}:{SelectedImage.Tag} ({SelectedImage.Id})?",
            "Xác nhận",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var req = new ImageRemoveRequest { Id = SelectedImage.Id };
            ApiActionResponse? res = await _apiClient.RemoveImageAsync(req).ConfigureAwait(true);
            if (res is null)
            {
                StatusMessage = "Không có phản hồi.";
                return;
            }

            if (!res.Ok)
            {
                StatusMessage = res.Error ?? "Xóa thất bại.";
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
            ComposeCommandResponse? res = await _apiClient.PruneImagesAsync(req).ConfigureAwait(true);
            ApplyPruneResult(res, "Prune image dangling");
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
        MessageBoxResult confirm = MessageBox.Show(
            "Xóa mọi image không được container nào sử dụng (docker image prune -a)? Thao tác không thể hoàn tác.",
            "Xác nhận",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var req = new ImagePruneRequest { AllUnused = true };
            ComposeCommandResponse? res = await _apiClient.PruneImagesAsync(req).ConfigureAwait(true);
            ApplyPruneResult(res, "Prune image -a");
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

    private void ApplyPruneResult(ComposeCommandResponse? res, string label)
    {
        if (res is null)
        {
            StatusMessage = $"{label}: không có phản hồi.";
            return;
        }

        if (!res.Ok)
        {
            StatusMessage = $"{label}: {res.Error ?? "lỗi"}";
            if (!string.IsNullOrEmpty(res.Output))
            {
                StatusMessage += Environment.NewLine + res.Output;
            }

            return;
        }

        StatusMessage = $"{label} thành công." + (string.IsNullOrEmpty(res.Output) ? string.Empty : Environment.NewLine + res.Output);
    }
}
