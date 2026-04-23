# Bảo mật khi dùng DockLite qua mạng LAN

Tài liệu này bổ sung cho `README.md` và phần Cài đặt (tab Kết nối) trong ứng dụng Windows. Từ bản 2.0, `docklite-wsl` mặc định lắng nghe tại `127.0.0.1:17890` (chỉ trên cùng máy). Chỉ khi bạn cấu hình `DOCKLITE_ADDR` mở ra LAN (ví dụ `0.0.0.0:17890`) cùng `DOCKLITE_API_TOKEN` thì máy khác mới tới được API. Service Go dùng HTTP; khi bạn đổi base URL sang IP máy chủ (ví dụ `http://192.168.x.x:17890/`) thì lưu lượng giữa DockLite và service **không được mã hóa bằng TLS** trên đoạn đó (trừ khi bạn tự cấu hình HTTPS/ proxy).

## Rủi ro chính

- **Nghe lén (sniffing):** Trên cùng một mạng LAN, kẻ tấn công có thể đọc được nội dung HTTP nếu không có biện pháp bảo vệ lớp mạng khác.
- **Token API:** Khi bật `DOCKLITE_API_TOKEN` trên WSL, token phải khớp với ô «Token API» trong Cài đặt. Token vẫn là **bí mật**; nếu lộ qua HTTP không mã hóa, kẻ tấn công có thể gọi API thay bạn (tùy quyền của API).

## Khuyến nghị thực tế

1. **Chỉ tin cậy mạng nội bộ** mà bạn kiểm soát (nhà, lab). Tránh mở port service ra Internet trực tiếp.
2. **Tường lửa Windows và router:** Hạn chế ai được phép truy cập IP:port của máy chạy WSL/service.
3. **Nhu cầu TLS hoặc VPN:** Nếu cần đi qua mạng không tin cậy, dùng reverse proxy có HTTPS (nginx, Caddy, …) phía trước service, hoặc VPN (WireGuard, Tailscale, …) để mã hóa toàn bộ tunnel.
4. **Đổi token** nếu nghi ngờ lộ; cập nhật cả biến môi trường WSL và Cài đặt DockLite, rồi khởi động lại service nếu cần.

## Liên quan

- Token API và biến `DOCKLITE_API_TOKEN`: xem mục biến môi trường / bảo mật trong `README.md`.
- OpenAPI: `GET /api/openapi.json` trên cùng base URL đã cấu hình.
