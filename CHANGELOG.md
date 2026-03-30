# Nhật ký thay đổi DockLite

Định dạng dựa trên [Keep a Changelog](https://keepachangelog.com/vi/1.0.0/).

## [0.1.0] — 2026-03-30

### Thêm

- Cài đặt: nút **Điền IP WSL** (lấy IPv4 qua `wsl hostname -I`) khi `127.0.0.1` từ Windows không kết nối được tới service WSL2.
- Cài đặt: nút **Khởi động service WSL** gọi `wsl` với `bash -lc` và chờ `/api/health` (khoảng 90 giây); áp dụng địa chỉ trong ô trước khi chờ (không bắt buộc đã Lưu file để thử kết nối).
- Trang **Nhật ký ứng dụng**: xem log file trong `%LocalAppData%\DockLite\logs`, làm mới, mở thư mục.
- Ghi log: lưu cài đặt, đổi HttpClient, lỗi UI chưa bắt, kiểm tra kết nối thất bại.
- `HttpClient`: `SocketsHttpHandler` với `UseProxy = false` để tránh proxy hệ thống làm hỏng kết nối localhost/WSL.
- WSL: suy luận tên distro từ đường dẫn UNC `\\wsl.localhost\<distro>\...` hoặc `\\wsl$\<distro>\...` cho `wslpath` và `wsl -d` (khi ô Distro WSL để trống).
- Script `run-server.sh`: xử lý CRLF, tách `set -eu` / `set -o pipefail`, script PowerShell chuẩn hóa LF.
- `Start-DockLiteWsl.ps1`: dùng `bash -lc` thay cho `bash -c`.

### Sửa

- **HttpClient** bị dispose khi Lưu cài đặt: hoãn dispose client cũ; `TryEnsureRunningAsync` dùng `DockLiteHttpSession` thay vì giữ một tham chiếu HttpClient cố định.
- Thông báo lỗi kết nối: gợi ý WSL2 và IP thay cho chỉ `127.0.0.1`.

### Đã biết / hẹn phiên sau

- **Tự khởi động service khi mở DockLite** và **khởi động service Go qua nút từ Windows** trong một số cấu hình WSL/distro/đường dẫn vẫn chưa ổn định. Khuyến nghị phiên bản hiện tại: chạy service trong WSL thủ công (`bash scripts/run-server.sh` hoặc `./bin/docklite-wsl`) khi cần; các chức năng ứng dụng sau khi service đã chạy hoạt động bình thường. Việc làm cho tự khởi động và gọi WSL từ Windows đáng tin cậy hơn được ghi nhận cho **phiên bản tiếp theo**.

### Tài liệu

- README: gỡ lỗi WSL2, CRLF, IP WSL, `bash -lc`, distro từ UNC.
