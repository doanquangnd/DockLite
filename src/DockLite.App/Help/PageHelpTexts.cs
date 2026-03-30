using DockLite.App.ViewModels;

namespace DockLite.App.Help;

/// <summary>
/// Nội dung trợ giúp theo màn hình (tiếng Việt).
/// </summary>
internal static class PageHelpTexts
{
    public static (string ShortTitle, string Body) GetForCurrentPage(object? page)
    {
        return page switch
        {
            DashboardViewModel => ("Tổng quan", DashboardBody),
            ContainersViewModel => ("Container", ContainersBody),
            LogsViewModel => ("Log container", LogsBody),
            ComposeViewModel => ("Docker Compose", ComposeBody),
            ImagesViewModel => ("Image", ImagesBody),
            CleanupViewModel => ("Dọn dẹp", CleanupBody),
            AppDebugLogViewModel => ("Nhật ký ứng dụng", AppDebugLogBody),
            SettingsViewModel => ("Cài đặt", SettingsBody),
            null => ("DockLite", "Chưa có màn hình được chọn."),
            _ => ("DockLite", "Không có nội dung trợ giúp cho màn hình này."),
        };
    }

    private const string DashboardBody =
        "Màn Tổng quan hiển thị trạng thái service WSL (health) và thông tin Docker Engine đọc qua API.\n\n"
        + "Làm mới: tải lại dữ liệu từ service. Cần service Go trong WSL đang chạy và DockLite kết nối đúng địa chỉ trong Cài đặt.\n\n"
        + "Nếu không có phản hồi: kiểm tra Cài đặt (base URL, tự khởi động WSL), hoặc chạy tay wsl-docker-service trong WSL.";

    private const string ContainersBody =
        "Danh sách container theo Docker. Lọc theo trạng thái, tìm theo tên, image, ID.\n\n"
        + "Ô Chọn: chọn nhiều dòng để thao tác hàng loạt. Chọn tất cả / Bỏ chọn điều khiển các ô tick.\n\n"
        + "Start đã chọn / Stop đã chọn / Xóa đã chọn: chỉ áp dụng cho dòng đã tick; Start chỉ khi container đang dừng, Stop khi đang chạy.\n\n"
        + "Dòng highlight (một dòng): Start, Stop, Restart, Xóa, Tải chi tiết (inspect + stats). Phần mở rộng có thể bật stats realtime theo chu kỳ.\n\n"
        + "Top RAM / Top CPU: xem snapshot từ server (không phải danh sách đầy đủ).";

    private const string LogsBody =
        "Xem log stdout/stderr của container. Chọn container, chọn số dòng tail, có thể tìm trong log.\n\n"
        + "Tải container: nạp danh sách container để chọn. Tải tail: lấy một lần phần cuối log.\n\n"
        + "Theo dõi (WS): luồng log (nếu service hỗ trợ WebSocket). Xóa màn hình: xóa nội dung hiển thị.";

    private const string ComposeBody =
        "Quản lý các thư mục project Docker Compose và gọi lệnh compose qua service trong WSL.\n\n"
        + "Thêm thư mục project (Windows hoặc đường dẫn WSL), chọn project trong bảng, chọn service trong file compose.\n\n"
        + "Có thể start/stop service, xem log service, chạy exec không TTY. Output lệnh compose hiện ở khung dưới.\n\n"
        + "Làm mới danh sách: đồng bộ project đã lưu; compose up/down/ps: chạy trong WSL tại thư mục project.";

    private const string ImagesBody =
        "Danh sách image Docker. Tìm theo repository, tag, ID. Dùng ô Chọn để chọn nhiều image.\n\n"
        + "Xóa các dòng đã chọn / Xóa image (dòng highlight): gỡ image. Prune dangling / prune không dùng: gọi lệnh prune qua API (cẩn thận dữ liệu).\n\n"
        + "Chọn tất cả / Bỏ chọn: tương tự container; các nút chỉ nên dùng khi đã chọn dòng phù hợp.";

    private const string CleanupBody =
        "Gọi các lệnh docker prune trong WSL (container, image, volume, network, system prune). Mỗi lệnh có thể xóa dữ liệu vĩnh viễn.\n\n"
        + "Đọc kỹ cảnh báo trong README. System prune kèm volume đặc biệt nguy hiểm.\n\n"
        + "Output hiển thị kết quả từ service. Chạy khi bạn hiểu rõ hậu quả.";

    private const string AppDebugLogBody =
        "Đọc file log của chính ứng dụng DockLite (Windows) theo ngày, lọc category, mức, chuỗi.\n\n"
        + "Cột thời gian trên màn hình dùng múi giờ và định dạng trong Cài đặt — tab Hiển thị (sau khi Lưu).\n\n"
        + "Làm mới: tải lại; Mở thư mục log: mở Explorer; Sao chép chẩn đoán / Xuất log: hỗ trợ gửi lỗi khi báo cáo.\n\n"
        + "Đường dẫn thư mục log chỉ để xem (read-only).";

    private const string SettingsBody =
        "Trang Cài đặt chia tab: Kết nối (base URL, timeout HTTP), WSL và service (tự khởi động, đường dẫn, distro, thử uname/wslpath), Hiển thị (múi giờ, định dạng ngày giờ, xem trước), Chờ và health (thời gian chờ sau khi spawn WSL, chờ Start/Restart thủ công, timeout từng lần poll, khoảng cách poll).\n\n"
        + "Lưu ghi toàn bộ vào file cài đặt. Hàng nút dưới cùng: Build service (go build trong WSL), Start / Dừng / Restart, Kiểm tra kết nối.\n\n"
        + "Điền IP WSL khi localhost không forward được sang WSL2. Thời gian chờ health không còn cố định: chỉnh trong tab Chờ và health rồi Lưu.";
}
