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

        if (string.IsNullOrWhiteSpace(s.ServiceApiToken))
        {
            s.ServiceApiToken = null;
        }
        else
        {
            string tok = s.ServiceApiToken.Trim();
            if (tok.Length > 8192)
            {
                tok = tok.Substring(0, 8192);
            }

            s.ServiceApiToken = tok;
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

        string theme = (s.UiTheme ?? string.Empty).Trim();
        if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            s.UiTheme = "Dark";
        }
        else
        {
            s.UiTheme = "Light";
        }

        string lang = (s.UiLanguage ?? string.Empty).Trim();
        if (string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase))
        {
            s.UiLanguage = "en";
        }
        else
        {
            s.UiLanguage = "vi";
        }
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
