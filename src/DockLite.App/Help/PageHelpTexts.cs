using System.Windows;
using DockLite.App.Services;
using DockLite.App.ViewModels;

namespace DockLite.App.Help;

/// <summary>
/// Nội dung trợ giúp theo màn hình (UiStrings.vi/en.xaml, khóa Ui_Help_*).
/// </summary>
internal static class PageHelpTexts
{
    public static (string ShortTitle, string Body) GetForCurrentPage(object? page)
    {
        return page switch
        {
            DashboardViewModel => (R("Ui_Help_Dashboard_Title", "Tổng quan"), R("Ui_Help_Dashboard_Body", DashboardBody)),
            ContainersViewModel => (R("Ui_Help_Containers_Title", "Container"), R("Ui_Help_Containers_Body", ContainersBody)),
            LogsViewModel => (R("Ui_Help_Logs_Title", "Log container"), R("Ui_Help_Logs_Body", LogsBody)),
            ComposeViewModel => (R("Ui_Help_Compose_Title", "Docker Compose"), R("Ui_Help_Compose_Body", ComposeBody)),
            ImagesViewModel => (R("Ui_Help_Images_Title", "Image"), R("Ui_Help_Images_Body", ImagesBody)),
            NetworkVolumeViewModel => (R("Ui_Help_NetworkVolume_Title", "Mạng và volume"), R("Ui_Help_NetworkVolume_Body", NetworkVolumeBody)),
            CleanupViewModel => (R("Ui_Help_Cleanup_Title", "Dọn dẹp"), R("Ui_Help_Cleanup_Body", CleanupBody)),
            AppDebugLogViewModel => (R("Ui_Help_AppDebugLog_Title", "Nhật ký ứng dụng"), R("Ui_Help_AppDebugLog_Body", AppDebugLogBody)),
            SettingsViewModel => (R("Ui_Help_Settings_Title", "Cài đặt"), R("Ui_Help_Settings_Body", SettingsBody)),
            null => (R("Ui_Help_DockLite_Title", "DockLite"), R("Ui_Help_NoPage_Body", NoPageBody)),
            _ => (R("Ui_Help_DockLite_Title", "DockLite"), R("Ui_Help_Unknown_Body", UnknownPageBody)),
        };
    }

    /// <summary>
    /// Liên kết mở bằng trình duyệt (OpenAPI theo base URL hiện tại, tài liệu ngoài tùy màn).
    /// </summary>
    public static IReadOnlyList<HelpHyperlink> GetHelpLinksForPage(object? page, Uri? apiBase)
    {
        var list = new List<HelpHyperlink>();
        if (apiBase is not null)
        {
            try
            {
                var openapi = new Uri(apiBase, "api/openapi.json");
                list.Add(
                    new HelpHyperlink(
                        UiLanguageManager.TryLocalizeCurrent("Ui_Help_Link_OpenApi", "Schema API (OpenAPI JSON)"),
                        openapi));
            }
            catch (UriFormatException)
            {
                // bỏ qua
            }
        }

        switch (page)
        {
            case SettingsViewModel:
                list.Add(
                    new HelpHyperlink(
                        UiLanguageManager.TryLocalizeCurrent("Ui_Help_Link_WslDocs", "Tài liệu WSL (Microsoft)"),
                        new Uri("https://learn.microsoft.com/windows/wsl/")));
                break;
            case ContainersViewModel:
                list.Add(
                    new HelpHyperlink(
                        UiLanguageManager.TryLocalizeCurrent("Ui_Help_Link_DockerContainerRef", "Tham chiếu lệnh docker container"),
                        new Uri("https://docs.docker.com/reference/cli/docker/container/")));
                break;
            case ComposeViewModel:
                list.Add(
                    new HelpHyperlink(
                        UiLanguageManager.TryLocalizeCurrent("Ui_Help_Link_ComposeDocs", "Tài liệu Docker Compose"),
                        new Uri("https://docs.docker.com/compose/")));
                break;
            case ImagesViewModel:
                list.Add(
                    new HelpHyperlink(
                        UiLanguageManager.TryLocalizeCurrent("Ui_Help_Link_DockerImagesRef", "Tham chiếu lệnh docker images"),
                        new Uri("https://docs.docker.com/reference/cli/docker/image/")));
                break;
            case LogsViewModel:
                list.Add(
                    new HelpHyperlink(
                        UiLanguageManager.TryLocalizeCurrent("Ui_Help_Link_DockerLogsRef", "Tham chiếu docker logs"),
                        new Uri("https://docs.docker.com/reference/cli/docker/container/logs/")));
                break;
            case NetworkVolumeViewModel:
                list.Add(
                    new HelpHyperlink(
                        UiLanguageManager.TryLocalizeCurrent("Ui_Help_Link_DockerNetworkRef", "Tài liệu Docker network"),
                        new Uri("https://docs.docker.com/network/")));
                break;
            case CleanupViewModel:
                list.Add(
                    new HelpHyperlink(
                        UiLanguageManager.TryLocalizeCurrent("Ui_Help_Link_DockerSystemPrune", "docker system prune"),
                        new Uri("https://docs.docker.com/reference/cli/docker/system/prune/")));
                break;
            case DashboardViewModel:
                list.Add(
                    new HelpHyperlink(
                        UiLanguageManager.TryLocalizeCurrent("Ui_Help_Link_DockerDocs", "Tài liệu Docker Engine"),
                        new Uri("https://docs.docker.com/engine/")));
                break;
        }

        return list;
    }

    private static string R(string key, string fallbackVi)
    {
        return UiLanguageManager.TryLocalize(Application.Current, key, fallbackVi);
    }

    private const string DashboardBody =
        "Màn Tổng quan hiển thị trạng thái service WSL (health) và thông tin Docker Engine đọc qua API.\n\n"
        + "Làm mới: tải lại dữ liệu từ service. Cần service Go trong WSL đang chạy và DockLite kết nối đúng địa chỉ trong Cài đặt.\n\n"
        + "Nếu không có phản hồi: kiểm tra Cài đặt (base URL, tự khởi động WSL), hoặc chạy tay wsl-docker-service trong WSL.\n\n"
        + "Phím F5 làm mới nội dung trang đang mở (trừ Cài đặt và Dọn dẹp).";

    private const string ContainersBody =
        "Danh sách container theo Docker. Lọc theo trạng thái, tìm theo tên, image, ID.\n\n"
        + "Ô Chọn: chọn nhiều dòng để thao tác hàng loạt. Chọn tất cả / Bỏ chọn điều khiển các ô tick.\n\n"
        + "Start đã chọn / Stop đã chọn / Xóa đã chọn / Sao chép ID đã chọn: chỉ áp dụng cho dòng đã tick; Start chỉ khi container đang dừng, Stop khi đang chạy.\n\n"
        + "Dòng highlight (một dòng): Start, Stop, Restart, Xóa, Tải chi tiết (inspect + stats). Phần mở rộng có thể bật stats realtime: polling REST theo chu kỳ hoặc WebSocket stream (ít request hơn). Interval WebSocket (ms) có thể chọn cao hơn khi nhiều container hoặc mạng chậm để giảm tải.\n\n"
        + "Sparkline CPU và RAM %: cập nhật khi realtime bật (hoặc một điểm sau «Tải chi tiết»); trục ngang là thời gian (mới nhất bên phải), tối đa 90 điểm.\n\n"
        + "Top RAM / Top CPU: xem snapshot từ server (không phải danh sách đầy đủ).\n\n"
        + "Phím F5 làm mới danh sách container khi đang ở màn Container.";

    private const string LogsBody =
        "Xem log stdout/stderr của container. Chọn container, chọn số dòng tail, có thể tìm trong log.\n\n"
        + "Ô Tìm: với dòng rất dài, chỉ khớp trong phần đầu (xem tooltip). Màu dòng theo từ khóa mức log cũng chỉ xét phần đầu dòng để tránh tải nặng.\n\n"
        + "Tải container: nạp danh sách container để chọn. Tải tail: lấy một lần phần cuối log.\n\n"
        + "Theo dõi (WS): luồng log; tần suất đưa dòng lên màn hình tự giãn khi bộ đệm lớn hoặc xử lý một đợt lâu. Xóa màn hình: xóa nội dung hiển thị.";

    private const string ComposeBody =
        "Quản lý các thư mục project Docker Compose và gọi lệnh compose qua service trong WSL.\n\n"
        + "Thêm thư mục project (Windows hoặc đường dẫn WSL), chọn project trong bảng, chọn service trong file compose.\n\n"
        + "Nhiều file compose: nhập mỗi dòng một đường dẫn tương đối trong thư mục project (tương đương nhiều lần -f trên CLI); service chấp nhận tối đa 16 file; có thể chỉnh sau khi thêm bằng «Lưu file compose».\n\n"
        + "Có thể start/stop service, xem log service, chạy exec không TTY. Output lệnh compose hiện ở khung dưới.\n\n"
        + "«Mở terminal WSL trong thư mục project»: gọi wsl.exe (theo distro trong Cài đặt nếu có) để mở bash tại thư mục project — dùng cho lệnh compose exec -it hoặc shell tương tác ngoài DockLite.\n\n"
        + "Làm mới danh sách: đồng bộ project đã lưu; compose up/down/ps: chạy trong WSL tại thư mục project.";

    private const string ImagesBody =
        "Danh sách image Docker. Tìm theo repository, tag, ID. Dùng ô Chọn để chọn nhiều image.\n\n"
        + "Khối mở rộng: inspect JSON, history layer, pull theo reference (log có thể rút gọn), xuất tar (docker save) và nhập tar (docker load). Pull, xuất và nhập chạy trên luồng nền để giao diện không đứng; vẫn có thể chờ lâu tới khi server phản hồi xong.\n\n"
        + "Xóa các dòng đã chọn / Xóa image (dòng highlight): gỡ image. Sao chép ID đã chọn: copy ID đầy đủ các dòng đã tick. Prune dangling / prune không dùng: gọi lệnh prune qua API (cẩn thận dữ liệu).\n\n"
        + "Chọn tất cả / Bỏ chọn: tương tự container; các nút chỉ nên dùng khi đã chọn dòng phù hợp.";

    private const string NetworkVolumeBody =
        "Liệt kê network và volume từ Docker Engine (GET /api/networks và /api/volumes). Làm mới để tải lại hai bảng.\n\n"
        + "Xóa volume: chọn một dòng trong bảng Volume, nhấn «Xóa volume», xác nhận. Volume đang được container dùng sẽ không xóa được (docker volume rm). Network: chỉ xem; tạo/xóa network qua CLI hoặc Compose.";

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
        "Trang Cài đặt chia tab: Kết nối (base URL, token API tùy chọn khớp DOCKLITE_API_TOKEN trên WSL, timeout HTTP), WSL và service (tự khởi động, thư mục dịch vụ, distro, nguồn Windows và đích Unix khi đồng bộ mã Go, tùy chọn version VERSION, thử uname/wslpath), Hiển thị (múi giờ, định dạng ngày giờ, xem trước), Chờ và health (thời gian chờ sau khi spawn WSL, chờ Start/Restart thủ công, timeout từng lần poll, khoảng cách poll).\n\n"
        + "Lưu ghi toàn bộ vào file cài đặt. Hàng nút dưới cùng: Build service (go build trong WSL), Start / Dừng / Restart, Kiểm tra kết nối.\n\n"
        + "Điền IP WSL khi localhost không forward được sang WSL2. Thời gian chờ health không còn cố định: chỉnh trong tab Chờ và health rồi Lưu.\n\n"
        + "Khi lỗi kết nối hoặc WSL: xem README trong repo (build service, biến môi trường, endpoint GET /api/openapi.json); bảo mật LAN và proxy: docs/docklite-lan-security.md; tạo token API: docs/docklite-api-token.md.\n\n"
        + "Trên màn Cài đặt, F5 không chạy kiểm tra mạng — dùng «Kiểm tra kết nối» hoặc «Lưu» (sau Lưu có thể kiểm tra /api/health trong nền).";

    private const string NoPageBody = "Chưa có màn hình được chọn.";

    private const string UnknownPageBody = "Không có nội dung trợ giúp cho màn hình này.";
}
