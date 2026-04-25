using System.Collections.Generic;
using DockLite.App.Help;

namespace DockLite.App.Services;

/// <summary>
/// Hộp thoại xác nhận (tách khỏi ViewModel để dễ test).
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Hiển thị Yes/No; trả về true nếu người dùng chọn Yes.
    /// </summary>
    Task<bool> ConfirmAsync(string message, string title, DialogConfirmKind kind = DialogConfirmKind.Question);

    /// <summary>
    /// Hộp thoại chỉ có nút OK (thông tin).
    /// </summary>
    Task ShowInfoAsync(string message, string title);

    /// <summary>
    /// Trợ giúp: nội dung văn bản và (tuỳ chọn) liên kết mở bằng trình duyệt.
    /// </summary>
    Task ShowHelpAsync(string body, string title, IReadOnlyList<HelpHyperlink>? links = null);

    /// <summary>
    /// Hộp thoại kết nối TLS lần đầu (fingerprint, subject, hiệu lực). Trả về true nếu người dùng chọn «Tin cậy».
    /// </summary>
    Task<bool> TlsFirstTrustFromDialogAsync(string message, string title);

    /// <summary>
    /// Hộp thoại cảnh báo khi cert đổi so với pin đã lưu. Trả về true nếu ghi lại pin mới («Tin chứng mới»).
    /// </summary>
    Task<bool> TlsCertificateChangedFromDialogAsync(string message, string title);
}
