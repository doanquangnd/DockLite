using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Services;
using DockLite.Contracts.Api;

namespace DockLite.App.ViewModels;

/// <summary>
/// Liệt kê network và volume Docker (GET /api/networks, /api/volumes); xóa volume (POST /api/volumes/remove).
/// </summary>
public partial class NetworkVolumeViewModel : ObservableObject
{
    private readonly INetworkVolumeScreenApi _networkVolumeApi;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly IAppShutdownToken _shutdownToken;
    private readonly AppShellActivityState _shellActivity;
    private readonly SemaphoreSlim _networkVolumeRefreshGate = new(1, 1);

    public NetworkVolumeViewModel(
        INetworkVolumeScreenApi networkVolumeApi,
        IDialogService dialogService,
        INotificationService notificationService,
        IAppShutdownToken shutdownToken,
        AppShellActivityState shellActivity)
    {
        _networkVolumeApi = networkVolumeApi;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _shutdownToken = shutdownToken;
        _shellActivity = shellActivity;
    }

    /// <summary>Sau khi làm mới, không có mạng nào.</summary>
    public bool ShowEmptyNetworksHint => !IsBusy && Networks.Count == 0;

    /// <summary>Sau khi làm mới, không có volume nào.</summary>
    public bool ShowEmptyVolumesHint => !IsBusy && Volumes.Count == 0;

    /// <summary>Có thể xóa volume đang chọn (không trong lúc tải danh sách).</summary>
    public bool CanRemoveVolume => SelectedVolume is not null && !IsBusy;

    private void NotifyEmptyNetworkVolumeHints()
    {
        OnPropertyChanged(nameof(ShowEmptyNetworksHint));
        OnPropertyChanged(nameof(ShowEmptyVolumesHint));
    }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private VolumeSummaryDto? _selectedVolume;

    public ObservableCollection<NetworkSummaryDto> Networks { get; } = new();

    public ObservableCollection<VolumeSummaryDto> Volumes { get; } = new();

    partial void OnSelectedVolumeChanged(VolumeSummaryDto? value)
    {
        OnPropertyChanged(nameof(CanRemoveVolume));
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyEmptyNetworkVolumeHints();
        OnPropertyChanged(nameof(CanRemoveVolume));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!_shellActivity.ShouldRefreshNetworkVolumeList)
        {
            return;
        }

        if (!await _networkVolumeRefreshGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            try
            {
                Networks.Clear();
                Volumes.Clear();
                ApiResult<NetworkListData> net = await _networkVolumeApi.GetNetworksAsync(_shutdownToken.Token).ConfigureAwait(true);
                if (!net.Success)
                {
                    string err = net.Error?.Message
                        ?? UiLanguageManager.TryLocalizeCurrent("Ui_NetVol_Status_NetworkLoadFailed", "Không đọc được mạng.");
                    StatusMessage = err;
                    _ = ApiErrorUiFeedback.ShowWarningToastAsync(_notificationService, err);
                    return;
                }

                foreach (NetworkSummaryDto n in net.Data?.Items ?? new List<NetworkSummaryDto>())
                {
                    Networks.Add(n);
                }

                ApiResult<VolumeListData> vol = await _networkVolumeApi.GetVolumesAsync(_shutdownToken.Token).ConfigureAwait(true);
                if (!vol.Success)
                {
                    string err = vol.Error?.Message
                        ?? UiLanguageManager.TryLocalizeCurrent("Ui_NetVol_Status_VolumeLoadFailed", "Không đọc được volume.");
                    StatusMessage = err;
                    _ = ApiErrorUiFeedback.ShowWarningToastAsync(_notificationService, err);
                    return;
                }

                foreach (VolumeSummaryDto v in vol.Data?.Items ?? new List<VolumeSummaryDto>())
                {
                    Volumes.Add(v);
                }

                StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                    "Ui_NetVol_Status_LoadedCountFormat",
                    "Đã tải {0} mạng, {1} volume.",
                    Networks.Count,
                    Volumes.Count);
            }
            catch (Exception ex)
            {
                StatusMessage = NetworkErrorMessageMapper.FormatForUser(ex);
                _ = ApiErrorUiFeedback.ShowNetworkExceptionToastAsync(_notificationService, ex);
            }
            finally
            {
                IsBusy = false;
                NotifyEmptyNetworkVolumeHints();
            }
        }
        finally
        {
            _networkVolumeRefreshGate.Release();
        }
    }

    [RelayCommand]
    private async Task RemoveVolume()
    {
        if (SelectedVolume is null)
        {
            return;
        }

        string volName = SelectedVolume.Name;
        bool confirmed = await _dialogService
            .ConfirmAsync(
                UiLanguageManager.TryLocalizeFormatCurrent(
                    "Ui_NetVol_RemoveVolumeConfirmFormat",
                    "Xóa volume «{0}»? Thao tác không hoàn tác. Volume đang được container dùng sẽ không xóa được.",
                    volName),
                UiLanguageManager.TryLocalizeCurrent("Ui_NetVol_RemoveVolumeTitle", "Xóa volume"),
                DialogConfirmKind.Warning)
            .ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        try
        {
            var req = new VolumeRemoveRequest { Name = volName };
            ApiResult<EmptyApiPayload> res = await _networkVolumeApi.RemoveVolumeAsync(req, _shutdownToken.Token).ConfigureAwait(true);
            if (!res.Success)
            {
                string err = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_NetVol_Status_VolumeRemoveFailed", "Không xóa được volume.");
                StatusMessage = err;
                _ = ApiErrorUiFeedback.ShowWarningToastAsync(_notificationService, err);
                return;
            }

            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_NetVol_Status_VolumeRemoved", "Đã xóa volume.");
            SelectedVolume = null;
            await RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = NetworkErrorMessageMapper.FormatForUser(ex);
            _ = ApiErrorUiFeedback.ShowNetworkExceptionToastAsync(_notificationService, ex);
        }
    }
}
