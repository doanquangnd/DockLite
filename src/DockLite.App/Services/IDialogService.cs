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
}
