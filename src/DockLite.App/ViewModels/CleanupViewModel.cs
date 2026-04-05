using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Services;
using DockLite.Contracts.Api;

namespace DockLite.App.ViewModels;

/// <summary>
/// Dọn dẹp Docker qua docker * prune / system prune (WSL).
/// </summary>
public partial class CleanupViewModel : ObservableObject
{
    private readonly ICleanupScreenApi _cleanupApi;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly IAppShutdownToken _shutdownToken;

    public CleanupViewModel(
        ICleanupScreenApi cleanupApi,
        IDialogService dialogService,
        INotificationService notificationService,
        IAppShutdownToken shutdownToken)
    {
        _cleanupApi = cleanupApi;
        _dialogService = dialogService;
        _notificationService = notificationService;
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
            ApiResult<ComposeCommandData> res = await _cleanupApi.SystemPruneAsync(req, _shutdownToken.Token).ConfigureAwait(true);
            ApplyResult(res, kind);
        }
        catch (Exception ex)
        {
            StatusMessage = NetworkErrorMessageMapper.FormatForUser(ex);
            CommandOutput = string.Empty;
            _ = ApiErrorUiFeedback.ShowNetworkExceptionToastAsync(_notificationService, ex);
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
            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Status_Common_LabelColonMessageFormat",
                "{0}: {1}",
                label,
                res.Error?.Message ?? UiLanguageManager.TryLocalizeCurrent("Ui_Status_Common_ErrorGeneric", "lỗi"));
            return;
        }

        CommandOutput = res.Data?.Output ?? string.Empty;
        StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
            "Ui_Status_Common_LabelDoneFormat",
            "{0}: hoàn tất.",
            label);
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
