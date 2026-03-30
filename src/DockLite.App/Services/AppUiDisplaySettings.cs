using System.Globalization;
using DockLite.Core.Configuration;

namespace DockLite.App.Services;

/// <summary>
/// Định dạng ngày giờ theo múi giờ và chuỗi format trong cài đặt (áp dụng sau Lưu hoặc khi load).
/// </summary>
public sealed class AppUiDisplaySettings
{
    private TimeZoneInfo _timeZone = TimeZoneInfo.Local;
    private string _dateTimeFormat = "yyyy/MM/dd HH:mm:ss";

    public void Apply(AppSettings settings)
    {
        AppSettings copy = settings;
        AppSettingsDefaults.Normalize(copy);
        _timeZone = ResolveTimeZone(copy.UiTimeZoneId);
        _dateTimeFormat = copy.UiDateTimeFormat.Trim();
    }

    /// <summary>
    /// Định dạng UTC theo cấu hình hiện đang áp dụng (sau <see cref="Apply"/>).
    /// </summary>
    public string FormatFromUtc(DateTime utc)
    {
        try
        {
            DateTime u = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            DateTime local = TimeZoneInfo.ConvertTimeFromUtc(u, _timeZone);
            return local.ToString(_dateTimeFormat, CultureInfo.CurrentCulture);
        }
        catch
        {
            return utc.ToString("O", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Xem trước không ghi đè trạng thái toàn app (dùng khi gõ trong Cài đặt).
    /// </summary>
    public string PreviewFormatUtc(DateTime utc, string? uiTimeZoneId, string? dateTimeFormat)
    {
        TimeZoneInfo tz = ResolveTimeZone(string.IsNullOrWhiteSpace(uiTimeZoneId) ? null : uiTimeZoneId.Trim());
        string fmt = string.IsNullOrWhiteSpace(dateTimeFormat) ? "yyyy/MM/dd HH:mm:ss" : dateTimeFormat.Trim();
        try
        {
            DateTime u = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            DateTime local = TimeZoneInfo.ConvertTimeFromUtc(u, tz);
            return local.ToString(fmt, CultureInfo.CurrentCulture);
        }
        catch
        {
            return utc.ToString("O", CultureInfo.InvariantCulture);
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Local;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
        }
        catch
        {
            return TimeZoneInfo.Local;
        }
    }
}
