using System.Security.Cryptography;
using System.Text;

namespace DockLite.Core.Security;

/// <summary>
/// SHA-256 nội dung DER (RawData) rồi biểu diễn hex, chữ hoa, phân tách bằng dấu hai chấm.
/// </summary>
public static class TlsCertificateFingerprint
{
    public static string Sha256ColonFromRaw(byte[] rawCert)
    {
        if (rawCert.Length == 0)
        {
            return string.Empty;
        }

        return BitConverter.ToString(SHA256.HashData(rawCert)).Replace("-", ":", StringComparison.Ordinal);
    }

    public static bool EqualsNormalized(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return false;
        }

        return string.Equals(StripToHex(a), StripToHex(b), StringComparison.OrdinalIgnoreCase);
    }

    public static string StripToHex(string colonOrDashFingerprint)
    {
        var sb = new StringBuilder(colonOrDashFingerprint.Length);
        foreach (char c in colonOrDashFingerprint)
        {
            if (c is ':' or ' ' or '-')
            {
                continue;
            }

            _ = sb.Append(c);
        }

        return sb.ToString();
    }
}
