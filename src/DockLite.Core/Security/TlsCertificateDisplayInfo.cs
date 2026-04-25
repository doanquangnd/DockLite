namespace DockLite.Core.Security;

/// <summary>
/// Thông tin hiển thị cho dialog TOFU / cert đổi (fingerprint, subject, hiệu lực).
/// </summary>
public sealed class TlsCertificateDisplayInfo
{
    public TlsCertificateDisplayInfo(
        string sha256FingerprintColon,
        string subject,
        string notBeforeLocal,
        string notAfterLocal)
    {
        Sha256FingerprintColon = sha256FingerprintColon;
        Subject = subject;
        NotBeforeLocal = notBeforeLocal;
        NotAfterLocal = notAfterLocal;
    }

    public string Sha256FingerprintColon { get; }

    public string Subject { get; }

    public string NotBeforeLocal { get; }

    public string NotAfterLocal { get; }
}
