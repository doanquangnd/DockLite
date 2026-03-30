using System.Globalization;
using System.Windows.Data;

namespace DockLite.App.Converters;

/// <summary>
/// Đảo giá trị bool (dùng cho IsEnabled khi đang bận).
/// </summary>
public sealed class InvertBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }

        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }

        return false;
    }
}
