namespace DockLite.Core.Configuration;

/// <summary>
/// Chuẩn hóa giá trị cài đặt sau khi đọc JSON (file cũ thiếu khóa hoặc giá trị ngoài biên).
/// </summary>
public static class AppSettingsDefaults
{
    public static void Normalize(AppSettings s)
    {
        if (string.IsNullOrWhiteSpace(s.ServiceBaseUrl))
        {
            s.ServiceBaseUrl = DockLiteDefaults.ServiceBaseUrl;
        }

        s.HttpTimeoutSeconds = Clamp(s.HttpTimeoutSeconds, 30, 600, 120);
        s.WslAutoStartHealthWaitSeconds = Clamp(s.WslAutoStartHealthWaitSeconds, 10, 600, 45);
        s.WslManualHealthWaitSeconds = Clamp(s.WslManualHealthWaitSeconds, 10, 600, 90);
        s.HealthProbeSingleRequestSeconds = Clamp(s.HealthProbeSingleRequestSeconds, 1, 60, 3);
        s.WslHealthPollIntervalMilliseconds = Clamp(s.WslHealthPollIntervalMilliseconds, 100, 5000, 500);

        if (string.IsNullOrWhiteSpace(s.UiDateTimeFormat))
        {
            s.UiDateTimeFormat = "dd/MM/yyyy HH:mm:ss";
        }

        string? tz = s.UiTimeZoneId?.Trim();
        s.UiTimeZoneId = string.IsNullOrEmpty(tz) ? null : tz;
    }

    private static int Clamp(int value, int min, int max, int fallback)
    {
        if (value < min || value > max)
        {
            return fallback;
        }

        return value;
    }
}
