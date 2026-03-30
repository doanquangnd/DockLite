namespace DockLite.Core.Configuration;

/// <summary>
/// Chuẩn hóa chuỗi base URL cho HttpClient.
/// </summary>
public static class ServiceBaseUriHelper
{
    public static Uri Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new Uri(DockLiteDefaults.ServiceBaseUrl);
        }

        var s = input.Trim();
        if (!s.EndsWith('/'))
        {
            s += "/";
        }

        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
        {
            return new Uri(DockLiteDefaults.ServiceBaseUrl);
        }

        return uri;
    }
}
