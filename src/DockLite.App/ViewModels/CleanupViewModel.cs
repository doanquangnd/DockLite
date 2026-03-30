using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.Contracts.Api;
using DockLite.Core;
using DockLite.Core.Services;

namespace DockLite.App.ViewModels;

/// <summary>
/// Dọn dẹp Docker qua docker * prune / system prune (WSL).
/// </summary>
public partial class CleanupViewModel : ObservableObject
{
    private readonly IDockLiteApiClient _apiClient;

    public CleanupViewModel(IDockLiteApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [ObservableProperty]
    private string _commandOutput = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Khi bật, lệnh system prune gửi kèm --volumes (xóa volume không dùng).
    /// </summary>
    [ObservableProperty]
    private bool _systemPruneIncludeVolumes;

    private async Task RunSystemPruneAsync(string kind, string confirmText, string title)
    {
        MessageBoxResult confirm = MessageBox.Show(
            confirmText,
            title,
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
            var req = new SystemPruneRequest
            {
                Kind = kind,
                WithVolumes = kind == "system" && SystemPruneIncludeVolumes,
            };
            ComposeCommandResponse? res = await _apiClient.SystemPruneAsync(req).ConfigureAwait(true);
            ApplyResult(res, kind);
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
            CommandOutput = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyResult(ComposeCommandResponse? res, string label)
    {
        if (res is null)
        {
            StatusMessage = $"{label}: không có phản hồi.";
            CommandOutput = string.Empty;
            return;
        }

        CommandOutput = res.Output ?? string.Empty;
        if (!res.Ok)
        {
            StatusMessage = $"{label}: {res.Error ?? "lỗi"}";
            return;
        }

        StatusMessage = $"{label}: hoàn tất.";
    }

    [RelayCommand]
    private Task PruneContainersAsync() => RunSystemPruneAsync(
        "containers",
        "Xóa container đã dừng (docker container prune -f)?",
        "Prune container");

    [RelayCommand]
    private Task PruneImagesAsync() => RunSystemPruneAsync(
        "images",
        "Xóa image dangling (docker image prune -f)?",
        "Prune image");

    [RelayCommand]
    private Task PruneVolumesAsync() => RunSystemPruneAsync(
        "volumes",
        "Xóa volume không được container nào gắn (docker volume prune -f)? Dữ liệu trong volume có thể mất vĩnh viễn.",
        "Prune volume");

    [RelayCommand]
    private Task PruneNetworksAsync() => RunSystemPruneAsync(
        "networks",
        "Xóa network không dùng (docker network prune -f)?",
        "Prune network");

    [RelayCommand]
    private Task PruneSystemAsync() => RunSystemPruneAsync(
        "system",
        SystemPruneIncludeVolumes
            ? "Chạy docker system prune -f --volumes? Có thể xóa image, container dừng, network và volume không dùng."
            : "Chạy docker system prune -f? Có thể xóa image, container dừng và network không dùng (không gồm volume).",
        "System prune");
}
