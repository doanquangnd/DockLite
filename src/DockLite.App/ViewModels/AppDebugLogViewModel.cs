using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.Core.Diagnostics;

namespace DockLite.App.ViewModels;

/// <summary>
/// Xem log file ứng dụng (gỡ lỗi).
/// </summary>
public partial class AppDebugLogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private string _logFolderPath = AppFileLog.LogDirectory;

    [RelayCommand]
    private void Refresh()
    {
        LoadRecent();
    }

    /// <summary>
    /// Tải lại nội dung log từ đĩa (gọi khi mở trang).
    /// </summary>
    public void LoadRecent()
    {
        LogText = AppFileLog.ReadRecentTail();
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppFileLog.LogDirectory,
                UseShellExecute = true,
            });
        }
        catch
        {
            // bỏ qua
        }
    }
}
