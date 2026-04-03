using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Services;
using DockLite.Contracts.Api;
using DockLite.Core.Services;

namespace DockLite.App.ViewModels;

/// <summary>
/// Phần tách: stats batch cho nhiều hàng đã chọn (POST /api/containers/stats-batch).
/// </summary>
public partial class ContainersViewModel
{
    /// <summary>
    /// Có ít nhất hai hàng đã chọn (tối đa 32) để gọi stats batch.
    /// </summary>
    [ObservableProperty]
    private bool _canBatchStats;

    /// <summary>
    /// Một lần lấy snapshot stats cho các container đã tick (2–32 id).
    /// </summary>
    [RelayCommand]
    private async Task BatchStatsSnapshotAsync()
    {
        IsBusy = true;
        try
        {
            List<string> ids = FilteredItems.Where(r => r.IsSelected).Select(r => r.Model.Id).Distinct().Take(32).ToList();
            if (ids.Count < 2)
            {
                StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Containers_Batch_Status_SelectAtLeastTwo",
                    "Chọn ít nhất hai container để dùng stats batch.");
                return;
            }

            ApiResult<ContainerStatsBatchData> r = await _apiClient
                .GetContainerStatsBatchAsync(ids, _shutdownToken.Token)
                .ConfigureAwait(true);
            if (!r.Success || r.Data is null)
            {
                StatusMessage = r.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent(
                        "Ui_Containers_Batch_Status_BatchFailed",
                        "Stats batch thất bại.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Stats batch ({r.Data.Items.Count} mục):");
            foreach (ContainerStatsBatchItemData it in r.Data.Items)
            {
                if (it.Ok && it.Stats is not null)
                {
                    string shortId = it.Id.Length <= 12 ? it.Id : it.Id[..12];
                    sb.Append(CultureInfo.InvariantCulture,
                        $"{shortId}: CPU {it.Stats.CpuUsagePercent:F1} % | RAM {FormatBytes(it.Stats.MemoryUsageBytes)}/{FormatBytes(it.Stats.MemoryLimitBytes)}");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine($"{it.Id}: lỗi — {it.Error}");
                }
            }

            StatusMessage = sb.ToString().TrimEnd();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Status_Common_CancelledShort", "Đã hủy.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
