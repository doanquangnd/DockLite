# Phase 1: Network Surface Reduction — Nghiên cứu kỹ thuật

**Gathered:** 2026-04-23
**Status:** Hoàn tất
**Mục tiêu phase:** Giảm bề mặt tấn công: bind loopback mặc định, fail-closed khi mở LAN mà không có token, WebSocket `CheckOrigin` chặn CSWSH, UI và `.env.example` phản ánh hành vi mới.

## Hiện trạng mã nguồn (bắt buộc đọc trước khi sửa)

| Khu vực | Tệp | Ghi chú |
|---------|-----|---------|
| Địa chỉ lắng nghe mặc định | `wsl-docker-service/cmd/server/main.go` | Mặc định `0.0.0.0:17890`, comment lý do forward WSL. |
| WebSocket nâng cấp | `wsl-docker-service/internal/wslimit/wslimit.go` | `CheckOrigin: return true` — cho phọ mọi origin. |
| Handlers WS | `wsl-docker-service/internal/ws/logs.go`, `wsl-docker-service/internal/docker/stats_ws.go` | Dùng `wslimit.Upgrader`. |
| Cảnh báo Base URL (WPF) | `src/DockLite.App/ViewModels/SettingsViewModel.cs` | `BuildNonLocalhostServiceUrlWarning` — cảnh báo host không phải loopback; màu cảnh báo từ `ThemeWarningForegroundBrush` trong `SettingsView.xaml`. |
| Mẫu env | `wsl-docker-service/.env.example` | Ghi `DOCKLITE_ADDR=0.0.0.0:17890`. |
| Mặc định client | `src/DockLite.Core/Configuration/DockLiteDefaults.cs` | `http://127.0.0.1:17890/`. |

## Ràng buộc từ REQUIREMENTS (NET-01 — NET-05)

- **NET-01:** Mặc định service bind `127.0.0.1:17890` thay vì `0.0.0.0:17890`.
- **NET-02:** Nếu địa chỉ lắng nghe **không** chỉ loopback và `DOCKLITE_API_TOKEN` rỗng thì từ chối khởi động, exit code 2, stderr có thông điệp rõ.
- **NET-03:** `CheckOrigin` mặc định chỉ chấp nhận origin tương ứng loopback (`localhost`, `127.0.0.1`, `::1`); mở rộng bằng `DOCKLITE_ALLOWED_ORIGINS` (ví dụ chuỗi phân tách bằng dấu phẩy).
- **NET-04:** UI Settings: banner cảnh báo khi non-loopback; phân tầng màu theo `ROADMAP` (cảnh báo vàng so với nghiêm trọng khi **HTTP** trên mạng không phải loopback — đỏ); tương phản tối thiểu AA sáng/tối.
- **NET-05:** Cập nhật `.env.example` và tài liệu liên quan: mặc định loopback, nêu rõ LAN là opt-in và cần token.

## Phương án thi hành (Go)

### 1) Phát hiện "bind cần token"

- Dùng `net.ResolveTCPAddr("tcp", addr)` (hoặc tách `host, port, err := net.SplitHostPort(addr)` rồi `net.LookupIP` nếu cần).
- Các trường hợp cần token khi chuỗi token rỗng:
  - IP của listener là `IsUnspecified()` (`0.0.0.0`, `[::]`) vì lắng nghe tất cả giao diện.
  - IP không thuộc loopback (ví dụ IP LAN).
- Trường hợp an toàn không cần token: chỉ lắng nghe trên `127.0.0.1` hoặc `::1` (tùy nền tảng, kiểm tra cả dạng IPv4/IPv6).
- Tách hàm thuần (ví dụ `internal/httpserver` hoặc `internal/listenpolicy`) + table-driven test, `main` gọi trước `ListenAndServe`, `os.Exit(2)` kèm `slog`/`log` stderr (theo thói quen hiện tại).

### 2) Đổi mặc định `addr`

- Hằng số mặc định: `127.0.0.1:17890` (giữ cùng cổng với client mặc định WPF).
- Cập nhật comment: giải thích WPF trên Windows tới WSL2 thường dùng `127.0.0.1` hoặc `http://<WSL-IP>:port` theo tài liệu; mở `0.0.0.0` là opt-in qua `DOCKLITE_ADDR` + bắt buộc token (NET-02).

### 3) `CheckOrigin` an toàn

- Dùng `url.Parse(Origin)`: nếu header `Origin` rỗng, cho phép (client không trình duyệt / một số client .NET) để tránh cắt kết nối hợp lệ.
- Nếu `Origin` có giá trị: kiểm tra `Hostname()` thuộc tập mặc định {`localhost`, `127.0.0.1`, `::1`} (so sánh chuỗi đã chuẩn hóa); thêm từ `DOCKLITE_ALLOWED_ORIGINS` (mỗi mục là URL gốc hoặc `scheme://host:port` — quy ước cần ghi rõ trong `.env.example`).
- Phản từ chối: có thể trả về `false` để Gorilla từ chối upgrade (HTTP 403) — bổ sung test với `httptest` tạo request giả `Origin: http://evil.com`.

## Phương án thi hành (WPF + XAML)

- Tách mức nghiêm trọng màu (đỏ so với vàng) bằng cách dùng thêm brush `ThemeDangerForegroundBrush` (hoặc tên tương đương) trong `ModernTheme.xaml` và `DarkTheme.xaml` với màu đạt tương phản tối thiểu 4.5:1 nền nội dung tương ứng.
- Logic: (a) nếu host không loopback **và** scheme `http` (hoặc `ws` nếu có): dùng cảnh báo đỏ (cleartext trên mạng); (b) nếu host không loopback **và** `https` (hoặc `wss`): cảnh báo vàng; (c) loopback: không cảnh báo (chuỗi rỗng) hoặc gợi ý tách `ServiceBaseUrlPortHint` giữ nguyên.
- `SettingsView.xaml`: ràng `Foreground` theo mức (DataTrigger theo thuộc tính mới, ví dụ `ServiceBaseUrlSecuritySeverity` enum hoặc `ServiceBaseUrlSecurityBrushKey`), hoặc hai `TextBlock` với `Visibility` loại trừ để dễ kiểm tra.

## Ghi chú tích hợp WSL2

- Người dùng từng cần `0.0.0.0` vì thử nghiệm chuyển tiếp từ Windows. Sau NET-01, luồng "LAN" vẫn bật bằng `DOCKLITE_ADDR=0.0.0.0:...` + bắt buộc `DOCKLITE_API_TOKEN` (NET-02). Tài liệu cần một đoạn "migration" ngắn (breaking change v2.0).

## Tài sản cần cập nhật

- `wsl-docker-service/.env.example` (luôn track được).
- `docs/docklite-lan-security.md` nếu tồn tại trên kho: đồng bộ; nếu thư mục `docs/*.md` bị gitignore, ghi ràng trong kế hoạch thực thi: `git add -f docs/...` khi cần.

## RESEARCH COMPLETE

Phần này cung cấp đủ cơ sở để viết PLAN 01-01 (Go) và 01-02 (WPF + tài liệu) mà không cần thu thập thêm thư viện bên ngoài.
