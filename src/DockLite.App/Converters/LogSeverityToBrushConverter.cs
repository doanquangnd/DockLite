using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DockLite.App.Models;

namespace DockLite.App.Converters;

/// <summary>
/// Ánh xạ <see cref="LogSeverity"/> sang màu chữ.
/// </summary>
public sealed class LogSeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LogSeverity s)
        {
            return Brushes.Black;
        }

        return s switch
        {
            LogSeverity.Error => new SolidColorBrush(Color.FromRgb(180, 0, 0)),
            LogSeverity.Warn => new SolidColorBrush(Color.FromRgb(160, 100, 0)),
            LogSeverity.Info => new SolidColorBrush(Color.FromRgb(0, 90, 140)),
            LogSeverity.Debug => new SolidColorBrush(Color.FromRgb(90, 90, 90)),
            _ => Brushes.Black,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
