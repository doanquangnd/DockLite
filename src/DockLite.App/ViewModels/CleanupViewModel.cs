using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Services;
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
    private readonly IDialogService _dialogService;
    private readonly IAppShutdownToken _shutdownToken;

    public CleanupViewModel(IDockLiteApiClient apiClient, IDialogService dialogService, IAppShutdownToken shutdownToken)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;
        _shutdownToken = shutdownToken;
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
        if (!await _dialogService.ConfirmAsync(confirmText, title, DialogConfirmKind.Warning).ConfigureAwait(true))
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
            ApiResult<ComposeCommandData> res = await _apiClient.SystemPruneAsync(req, _shutdownToken.Token).ConfigureAwait(true);
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

    private void ApplyResult(ApiResult<ComposeCommandData> res, string label)
    {
        if (!res.Success)
        {
            CommandOutput = res.Error?.Details ?? string.Empty;
            StatusMessage = $"{label}: {res.Error?.Message ?? "lỗi"}";
            return;
        }

        CommandOutput = res.Data?.Output ?? string.Empty;
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
