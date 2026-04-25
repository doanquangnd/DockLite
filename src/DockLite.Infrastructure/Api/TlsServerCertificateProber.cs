using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using DockLite.Core.Security;

namespace DockLite.Infrastructure.Api;

/// <summary>
/// Kết nối TLS tới host:port, chấp nhận mọi cert để đọc fingerprint (dùng trong màn hình Cài đặt trước khi lưu pin).
/// </summary>
public static class TlsServerCertificateProber
{
    public static async Task<TlsCertificateDisplayInfo?> ProbeAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(host) || port < 1 || port > 65535)
        {
            return null;
        }

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            using var tcp = new TcpClient();
            await tcp
                .ConnectAsync(host, port, connectCts.Token)
                .ConfigureAwait(false);

            using NetworkStream network = tcp.GetStream();
            using var ssl = new SslStream(
                network,
                false,
                (_, _, _, _) => true, // màn hình cài đặt: chấp nhận tạm để đọc fingerprint, không dùng cho traffic thật
                null!);

            var opts = new SslClientAuthenticationOptions
            {
                TargetHost = host,
            };
            await ssl
                .AuthenticateAsClientAsync(opts, connectCts.Token)
                .ConfigureAwait(false);

            if (ssl.RemoteCertificate is null)
            {
                return null;
            }

            var cert2 = new X509Certificate2(ssl.RemoteCertificate);
            return BuildDisplayInfo(cert2);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static TlsCertificateDisplayInfo BuildDisplayInfo(X509Certificate2 c)
    {
        string fp = TlsCertificateFingerprint.Sha256ColonFromRaw(c.GetRawCertData());
        return new TlsCertificateDisplayInfo(
            fp,
            c.Subject,
            c.NotBefore.ToString("G", null),
            c.NotAfter.ToString("G", null));
    }
}
