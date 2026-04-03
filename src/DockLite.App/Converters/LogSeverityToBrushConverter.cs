using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DockLite.App.Models;

namespace DockLite.App.Converters;

/// <summary>
/// Ánh xạ <see cref="LogSeverity"/> sang màu chữ (ưu tiên brush theme nếu có).
/// </summary>
public sealed class LogSeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LogSeverity s)
        {
            return FindBrush("ThemeLogSeverityDefaultBrush") ?? new SolidColorBrush(Color.FromRgb(51, 65, 85));
        }

        string key = s switch
        {
            LogSeverity.Error => "ThemeLogSeverityErrorBrush",
            LogSeverity.Warn => "ThemeLogSeverityWarnBrush",
            LogSeverity.Info => "ThemeLogSeverityInfoBrush",
            LogSeverity.Debug => "ThemeLogSeverityDebugBrush",
            _ => "ThemeLogSeverityDefaultBrush",
        };

        return FindBrush(key) ?? new SolidColorBrush(Color.FromRgb(51, 65, 85));
    }

    private static object? FindBrush(string key)
    {
        return Application.Current?.TryFindResource(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
