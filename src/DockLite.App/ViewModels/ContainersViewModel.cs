using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.Contracts.Api;
using DockLite.Core;
using DockLite.Core.Services;

namespace DockLite.App.ViewModels;

/// <summary>
/// Danh sách container: làm mới, lọc, tìm, thao tác start/stop/restart/xóa.
/// </summary>
public partial class ContainersViewModel : ObservableObject
{
    private readonly IDockLiteApiClient _apiClient;
    private List<ContainerSummaryDto> _allItems = new();

    public ContainersViewModel(IDockLiteApiClient apiClient)
    {
        _apiClient = apiClient;
        UpdateToolbarState();
    }

    [ObservableProperty]
    private string _filterKind = "Tất cả";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ContainerSummaryDto? _selectedContainer;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canStart;

    [ObservableProperty]
    private bool _canStop;

    [ObservableProperty]
    private bool _canRestart;

    [ObservableProperty]
    private bool _canRemove;

    public ObservableCollection<ContainerSummaryDto> FilteredItems { get; } = new();

    /// <summary>
    /// Giá trị cho ComboBox lọc (khớp <see cref="FilterKind"/>).
    /// </summary>
    public IReadOnlyList<string> FilterKinds { get; } = new[] { "Tất cả", "Đang chạy", "Đã dừng" };

    partial void OnSelectedContainerChanged(ContainerSummaryDto? value)
    {
        UpdateToolbarState();
    }

    partial void OnFilterKindChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnIsBusyChanged(bool value)
    {
        UpdateToolbarState();
    }

    /// <summary>
    /// Tải lại từ API và áp dụng lọc/tìm.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            ContainerListResponse? list = await _apiClient.GetContainersAsync().ConfigureAwait(true);
            if (list is null)
            {
                StatusMessage = "Không đọc được danh sách.";
                _allItems = new List<ContainerSummaryDto>();
                ApplyFilter();
                return;
            }

            if (!string.IsNullOrEmpty(list.Error))
            {
                StatusMessage = list.Error;
            }

            _allItems = list.Items ?? new List<ContainerSummaryDto>();
            ApplyFilter();
            if (string.IsNullOrEmpty(list.Error))
            {
                StatusMessage = $"Đã tải {_allItems.Count} container.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
            _allItems = new List<ContainerSummaryDto>();
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
            UpdateToolbarState();
        }
    }

    [RelayCommand]
    private async Task StartSelectedAsync()
    {
        if (SelectedContainer is null)
        {
            return;
        }

        await RunActionAsync(() => _apiClient.StartContainerAsync(SelectedContainer.Id));
    }

    [RelayCommand]
    private async Task StopSelectedAsync()
    {
        if (SelectedContainer is null)
        {
            return;
        }

        await RunActionAsync(() => _apiClient.StopContainerAsync(SelectedContainer.Id));
    }

    [RelayCommand]
    private async Task RestartSelectedAsync()
    {
        if (SelectedContainer is null)
        {
            return;
        }

        await RunActionAsync(() => _apiClient.RestartContainerAsync(SelectedContainer.Id));
    }

    [RelayCommand]
    private async Task RemoveSelectedAsync()
    {
        if (SelectedContainer is null)
        {
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"Xóa container \"{SelectedContainer.Name}\" ({SelectedContainer.ShortId})?",
            "DockLite",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        bool force = IsRunning(SelectedContainer.Status);
        await RunActionAsync(() => _apiClient.RemoveContainerAsync(SelectedContainer.Id, force));
    }

    private async Task RunActionAsync(Func<Task<ApiActionResponse?>> call)
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            ApiActionResponse? res = await call().ConfigureAwait(true);
            if (res is null)
            {
                StatusMessage = "Không có phản hồi.";
                return;
            }

            if (!res.Ok)
            {
                StatusMessage = string.IsNullOrWhiteSpace(res.Error) ? "Thao tác thất bại." : res.Error;
                return;
            }

            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
        }
        finally
        {
            IsBusy = false;
            UpdateToolbarState();
        }
    }

    private void ApplyFilter()
    {
        FilteredItems.Clear();
        string q = SearchText.Trim();
        foreach (ContainerSummaryDto item in _allItems)
        {
            if (!MatchesFilterKind(item))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(q) && !MatchesSearch(item, q))
            {
                continue;
            }

            FilteredItems.Add(item);
        }
    }

    private bool MatchesFilterKind(ContainerSummaryDto item)
    {
        return FilterKind switch
        {
            "Đang chạy" => IsRunning(item.Status),
            "Đã dừng" => IsStopped(item.Status),
            _ => true,
        };
    }

    private static bool MatchesSearch(ContainerSummaryDto item, string q)
    {
        return item.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
               || item.ShortId.Contains(q, StringComparison.OrdinalIgnoreCase)
               || item.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
               || item.Image.Contains(q, StringComparison.OrdinalIgnoreCase)
               || item.Status.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRunning(string status)
    {
        return status.StartsWith("Up ", StringComparison.OrdinalIgnoreCase)
               || status.Equals("running", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStopped(string status)
    {
        return status.Contains("Exited", StringComparison.OrdinalIgnoreCase)
               || status.Contains("Created", StringComparison.OrdinalIgnoreCase)
               || status.Contains("Dead", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateToolbarState()
    {
        ContainerSummaryDto? s = SelectedContainer;
        if (s is null || IsBusy)
        {
            CanStart = false;
            CanStop = false;
            CanRestart = false;
            CanRemove = false;
            return;
        }

        bool running = IsRunning(s.Status);
        CanStart = !running;
        CanStop = running;
        CanRestart = true;
        CanRemove = true;
    }
}
