namespace DockLite.Core.Security;

/// <summary>
/// Lưu fingerprint SHA-256 (định dạng dấu hai chấm) của cert TLS theo từng service (host+port) trong Credential Manager.
/// </summary>
public interface ITrustedFingerprintStore
{
    /// <summary>Đọc fingerprint đang tin cậy; null nếu chưa pin.</summary>
    string? Read(string host, int port);

    void Write(string host, int port, string sha256HexColon);

    void Remove(string host, int port);
}
