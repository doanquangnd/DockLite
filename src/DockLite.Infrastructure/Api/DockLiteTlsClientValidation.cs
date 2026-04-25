using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using DockLite.Core.Security;

namespace DockLite.Infrastructure.Api;

/// <summary>
/// So khớp cert TLS từ kết nối với fingerprint đã lưu (TOFU) — tự ký: chỉ chấp nhận khi pin trùng.
/// </summary>
public static class DockLiteTlsClientValidation
{
    public static bool ServerCertificateValidatesPin(
        ITrustedFingerprintStore store,
        string host,
        int port,
        X509Certificate? cert,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        _ = chain;
        _ = sslPolicyErrors;
        if (cert is null)
        {
            return false;
        }

        try
        {
            byte[] raw = cert.GetRawCertData();
            if (raw.Length == 0)
            {
                return false;
            }

            string actual = TlsCertificateFingerprint.Sha256ColonFromRaw(raw);
            string? expected = store.Read(host, port);
            if (string.IsNullOrEmpty(expected))
            {
                return false;
            }

            return TlsCertificateFingerprint.EqualsNormalized(expected, actual);
        }
        catch
        {
            return false;
        }
    }
}
