using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Services;
using DockLite.Core.Diagnostics;
using Microsoft.Win32;

namespace DockLite.App.ViewModels;

/// <summary>
/// Xem log file ứng dụng (gỡ lỗi): lọc category, mức, xuất và sao chép chẩn đoán.
/// </summary>
public partial class AppDebugLogViewModel : ObservableObject
{
    private readonly AppUiDisplaySettings _uiDisplay;

    private enum LogLevelGuess
    {
        TatCa,
        Loi,
        CanhBao,
        ThongTin,
    }

    private string _rawLogText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _visibleLines = new();

    [ObservableProperty]
    private string _logFolderPath = AppFileLog.LogDirectory;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Lọc dòng chứa chuỗi này (không phân biệt hoa thường). Để trống để không lọc theo chuỗi.
    /// </summary>
    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary>
    /// "Tất cả" hoặc một category lấy từ cột thứ hai trong file (tab).
    /// </summary>
    [ObservableProperty]
    private string _selectedCategory = "Tất cả";

    /// <summary>
    /// Một trong: Tất cả, Lỗi, Cảnh báo, Thông tin (suy đoán từ nội dung dòng).
    /// </summary>
    [ObservableProperty]
    private string _selectedLevelOption = "Tất cả";

    public ObservableCollection<string> CategoryOptions { get; } = new();

    public ObservableCollection<string> LevelOptions { get; } = new()
    {
        "Tất cả",
        "Lỗi",
        "Cảnh báo",
        "Thông tin",
    };

    public AppDebugLogViewModel(AppUiDisplaySettings uiDisplay)
    {
        _uiDisplay = uiDisplay;
        CategoryOptions.Add("Tất cả");
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

    partial void OnSelectedLevelOptionChanged(string value) => ApplyFilter();

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
        _rawLogText = AppFileLog.ReadRecentTail();
        RebuildCategoryOptions();
        ApplyFilter();
        StatusMessage = string.Empty;
    }

    private void RebuildCategoryOptions()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in SplitLines(_rawLogText))
        {
            string? cat = TryParseCategory(line);
            if (!string.IsNullOrEmpty(cat))
            {
                set.Add(cat);
            }
        }

        CategoryOptions.Clear();
        CategoryOptions.Add("Tất cả");
        foreach (string c in set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            CategoryOptions.Add(c);
        }

        if (!CategoryOptions.Contains(SelectedCategory))
        {
            SelectedCategory = "Tất cả";
        }
    }

    private void ApplyFilter()
    {
        var kept = new List<string>();
        foreach (string line in SplitLines(_rawLogText))
        {
            if (!PassesCategory(line))
            {
                continue;
            }

            if (!PassesLevel(line))
            {
                continue;
            }

            if (!PassesTextFilter(line))
            {
                continue;
            }

            kept.Add(FormatLogLineForDisplay(line));
        }

        if (kept.Count == 0)
        {
            VisibleLines = new ObservableCollection<string> { "(Không có dòng khớp bộ lọc.)" };
            return;
        }

        VisibleLines = new ObservableCollection<string>(kept);
    }

    private bool PassesCategory(string line)
    {
        if (string.Equals(SelectedCategory, "Tất cả", StringComparison.Ordinal))
        {
            return true;
        }

        string? cat = TryParseCategory(line);
        return cat is not null
            && cat.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase);
    }

    private bool PassesLevel(string line)
    {
        LogLevelGuess sel = ParseLevelFromUi(SelectedLevelOption);
        if (sel == LogLevelGuess.TatCa)
        {
            return true;
        }

        LogLevelGuess g = GuessLevel(line);
        return sel == g;
    }

    private static LogLevelGuess ParseLevelFromUi(string s) => s switch
    {
        "Lỗi" => LogLevelGuess.Loi,
        "Cảnh báo" => LogLevelGuess.CanhBao,
        "Thông tin" => LogLevelGuess.ThongTin,
        _ => LogLevelGuess.TatCa,
    };

    private bool PassesTextFilter(string line)
    {
        string f = FilterText.Trim();
        if (string.IsNullOrEmpty(f))
        {
            return true;
        }

        return line.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static IEnumerable<string> SplitLines(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            yield break;
        }

        foreach (string line in raw.Replace("\r\n", "\n").Split('\n'))
        {
            yield return line;
        }
    }

    private static string? TryParseCategory(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }

        int i = line.IndexOf('\t');
        if (i < 0)
        {
            return null;
        }

        int j = line.IndexOf('\t', i + 1);
        if (j < 0)
        {
            return null;
        }

        return line.Substring(i + 1, j - i - 1);
    }

    private static LogLevelGuess GuessLevel(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return LogLevelGuess.ThongTin;
        }

        string low = line.ToLowerInvariant();
        if (line.Contains("Exception", StringComparison.Ordinal)
            || low.Contains("lỗi")
            || low.Contains("error")
            || low.Contains("failed")
            || low.Contains("thất bại"))
        {
            return LogLevelGuess.Loi;
        }

        if (low.Contains("warning")
            || low.Contains("timeout")
            || low.Contains("cảnh báo")
            || low.Contains("warn"))
        {
            return LogLevelGuess.CanhBao;
        }

        return LogLevelGuess.ThongTin;
    }

    /// <summary>
    /// Đổi cột thời gian đầu dòng (UTC ISO trong file) sang định dạng theo Cài đặt — Hiển thị.
    /// </summary>
    private string FormatLogLineForDisplay(string line)
    {
        if (string.IsNullOrEmpty(line) || line.StartsWith("(", StringComparison.Ordinal))
        {
            return line;
        }

        int t1 = line.IndexOf('\t');
        if (t1 <= 0)
        {
            return line;
        }

        ReadOnlySpan<char> tsSpan = line.AsSpan(0, t1);
        if (!DateTime.TryParse(tsSpan, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime utc))
        {
            return line;
        }

        if (utc.Kind == DateTimeKind.Unspecified)
        {
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        }
        else if (utc.Kind == DateTimeKind.Local)
        {
            utc = utc.ToUniversalTime();
        }

        return _uiDisplay.FormatFromUtc(utc) + line.Substring(t1);
    }

    /// <summary>
    /// Sao chép khối chẩn đoán (phiên bản, bộ lọc, đường dẫn log, UTC, nội dung đang hiển thị) vào clipboard.
    /// </summary>
    [RelayCommand]
    private void CopyDiagnostics()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("DockLite — thông tin chẩn đoán");
            sb.Append("Phiên bản: ").AppendLine(Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?");
            sb.Append("OS: ").AppendLine(Environment.OSVersion.ToString());
            sb.Append("Thư mục log: ").AppendLine(AppFileLog.LogDirectory);
            sb.Append("Thời gian (theo cài đặt hiển thị): ").AppendLine(_uiDisplay.FormatFromUtc(DateTime.UtcNow));
            sb.Append("Lọc category: ").AppendLine(SelectedCategory);
            sb.Append("Lọc mức: ").AppendLine(SelectedLevelOption);
            sb.Append("Lọc chuỗi: ").AppendLine(string.IsNullOrEmpty(FilterText.Trim()) ? "(trống)" : FilterText.Trim());
            sb.AppendLine();
            sb.AppendLine("--- Nội dung đang hiển thị ---");
            sb.Append(string.Join(Environment.NewLine, VisibleLines));
            System.Windows.Clipboard.SetText(sb.ToString());
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_AppLog_Status_CopyOk", "Đã sao chép vào clipboard.");
        }
        catch (Exception ex)
        {
            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_AppLog_Status_CopyFailedFormat",
                "Không sao chép được: {0}",
                ex.Message);
        }
    }

    /// <summary>
    /// Xuất log: nếu đang có bộ lọc (chuỗi / category / mức) thì ghi phần đang hiển thị; không thì ghi toàn bộ đuôi đã đọc.
    /// </summary>
    [RelayCommand]
    private void ExportLog()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Văn bản (*.txt)|*.txt|Tất cả|*.*",
            DefaultExt = ".txt",
            FileName = "docklite-log-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".txt",
        };
        if (dlg.ShowDialog() != true)
        {
            return;
        }

        try
        {
            bool anyFilter = FilterText.Trim().Length > 0
                || !string.Equals(SelectedCategory, "Tất cả", StringComparison.Ordinal)
                || ParseLevelFromUi(SelectedLevelOption) != LogLevelGuess.TatCa;
            string body = anyFilter
                ? string.Join(Environment.NewLine, VisibleLines.Where(static l => l != "(Không có dòng khớp bộ lọc.)"))
                : string.Join(Environment.NewLine, SplitLines(_rawLogText).Select(FormatLogLineForDisplay));
            File.WriteAllText(dlg.FileName, body, Encoding.UTF8);
            StatusMessage = anyFilter
                ? UiLanguageManager.TryLocalizeFormatCurrent(
                    "Ui_AppLog_Status_ExportFilteredFormat",
                    "Đã xuất phần đang lọc: {0}",
                    dlg.FileName)
                : UiLanguageManager.TryLocalizeFormatCurrent(
                    "Ui_AppLog_Status_ExportFullFormat",
                    "Đã xuất toàn bộ đuôi log: {0}",
                    dlg.FileName);
        }
        catch (Exception ex)
        {
            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_AppLog_Status_ExportFailedFormat",
                "Không ghi được file: {0}",
                ex.Message);
        }
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
