using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Services;
using DockLite.Contracts.Api;
using DockLite.Core;
using DockLite.Core.Services;

namespace DockLite.App.ViewModels;

/// <summary>
/// Liệt kê network và volume Docker (GET /api/networks, /api/volumes).
/// </summary>
public partial class NetworkVolumeViewModel : ObservableObject
{
    private readonly IDockLiteApiClient _apiClient;
    private readonly IAppShutdownToken _shutdownToken;

    public NetworkVolumeViewModel(IDockLiteApiClient apiClient, IAppShutdownToken shutdownToken)
    {
        _apiClient = apiClient;
        _shutdownToken = shutdownToken;
    }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<NetworkSummaryDto> Networks { get; } = new();

    public ObservableCollection<VolumeSummaryDto> Volumes { get; } = new();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            Networks.Clear();
            Volumes.Clear();
            ApiResult<NetworkListData> net = await _apiClient.GetNetworksAsync(_shutdownToken.Token).ConfigureAwait(true);
            if (!net.Success)
            {
                StatusMessage = net.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_NetVol_Status_NetworkLoadFailed", "Không đọc được mạng.");
                return;
            }

            foreach (NetworkSummaryDto n in net.Data?.Items ?? new List<NetworkSummaryDto>())
            {
                Networks.Add(n);
            }

            ApiResult<VolumeListData> vol = await _apiClient.GetVolumesAsync(_shutdownToken.Token).ConfigureAwait(true);
            if (!vol.Success)
            {
                StatusMessage = vol.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_NetVol_Status_VolumeLoadFailed", "Không đọc được volume.");
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
            StatusMessage = ExceptionMessages.FormatForUser(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
