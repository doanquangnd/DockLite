# DockLite - Đánh giá kỹ thuật và hướng mở rộng (cập nhật theo repo)

Tài liệu này bổ sung cho `docs/docklite-improvement-plan.md` và `CHANGELOG.md`. Các mục dưới đây phản ánh **trạng thái code hiện tại** (sau DI, Engine API, envelope REST).

---

## 1. Tổng quan thay đổi đã có

- Dependency Injection trong WPF (`AddDockLiteUi`, `IAppShellFactory`, `AppShellFactory`).
- Luồng khởi động tách khỏi `MainWindow` (`IAppStartupService` / `AppStartupCoordinator`).
- `build-server.sh` chỉ build; `run-server.sh` chỉ chạy `bin/docklite-wsl` (không `go mod tidy` mỗi lần).
- WSL auto-start: đệm stdout/stderr, gợi ý khi health timeout.
- API REST: envelope `ApiEnvelope<T>` / `ApiResult<T>`, HTTP status có nghĩa (không còn mọi lỗi đều 200 + `{ ok: false }` trên `/api/*` trừ health).
- Service Go: phần lớn thao tác container/image/info/prune/logs qua Docker Engine API (`internal/dockerengine`); compose vẫn gọi plugin `docker compose` (CLI).
- Compose: validate thư mục/file compose/trùng đường dẫn (Go); UI chọn thư mục; danh sách service, start/stop/logs; `exec -T` qua API.
- Container: inspect, snapshot stats (kể blkio), polling, Top 5 RAM/CPU, batch chọn hàng.
- ViewModel: `IDialogService` thay `MessageBox` trực tiếp ở các màn đã áp dụng; `IAppShutdownToken` trên nhiều VM gọi API/WebSocket dài.
- Settings: `AppSettings` đọc **một lần** trong `AppShellFactory.Create`, inject `AppSettings initialSettings` vào `SettingsViewModel` (không tự `Load()` lúc khởi tạo).
- Logs: batch WebSocket (~150 ms), tối ưu khi không search, `ListBox` ảo hóa + dòng không wrap.

---

## 2. Đối chiếu với bản đánh giá cũ (đã lỗi thời)

Các cảnh báo sau **từng đúng** nhưng **đã được xử lý hoặc làm một phần** trong repo hiện tại:

| Nội dung cũ | Trạng thái hiện tại |
|-------------|---------------------|
| `run-server.sh` có thể còn build | Script chỉ `exec` binary; thiếu binary thì báo lỗi và thoát. **Lưu ý:** `README.md` vẫn có đoạn gợi ý «đọc lỗi go mod / go build» khi chạy `run-server.sh` (dòng gỡ lỗi) — nên sửa README thành «chạy `build-server.sh` trước» cho khớp. |
| Settings không single source | Một lần `store.Load()` trong `AppShellFactory`, truyền snapshot vào VM. |
| LogsViewModel lag / Invoke liên tục | Đã có batch timer, `BeginInvoke`, tối ưu incremental; vẫn có thể tối ưu thêm nếu log cực lớn. |
| MessageBox trong ViewModel | Không còn `MessageBox` trong thư mục `ViewModels` (dùng `IDialogService`). |
| API lỗi không chuẩn HTTP | Envelope + mã HTTP; client dùng `ApiResult<T>`. |
| Go chỉ `exec docker` | Engine API cho luồng chính; compose vẫn CLI (theo thiết kế). |
| Compose thiếu validation / service | Đã có validation Go + thao tác theo service + logs/exec (không TTY). |

---

## 3. Vấn đề hoặc hạng mục còn lại (thực tế)

### 3.1 Tài liệu README

- Một số đoạn vẫn nói cần Go để **`run-server.sh` build** (ví dụ gần dòng gỡ lỗi). Nên thống nhất: build bằng `build-server.sh`, chạy bằng `run-server.sh`.

### 3.2 Stats realtime

- Đang snapshot + polling; có Top RAM/CPU và blkio trong JSON. **Chưa** có kênh WebSocket chuyên cho stats (chỉ logs qua WS).

### 3.3 Compose tương tác đầy đủ

- `docker compose exec -T` đã có; **shell tương tác `-it`** vẫn ngoài phạm vi UI (terminal bên ngoài).

### 3.4 Hủy tác vụ dài

- `IAppShutdownToken` đã gắn nhiều VM; vẫn có thể bổ sung khi thêm lệnh chạy lâu mới.

### 3.5 Kiểm tra sức khỏe WSL

- Đã có retry ngắn trước spawn; vòng chờ sau spawn vẫn có thể tinh chỉnh backoff.

---

## 4. Hướng mở rộng (phần chưa làm hoặc mở rộng nhẹ)

- Stream stats qua WebSocket (tùy chọn bên cạnh polling).
- Images: inspect/pull/tag/export nâng cao (một phần đã có prune/xóa batch).
- Diagnostics: log viewer đã có lọc/xuất; có thể bổ sung hiển thị block WSL gần nhất ngay trong UI (một phần đã có qua toast/log).

Chi tiết theo sprint có thể lấy từ `docs/docklite-improvement-plan.md` (bảng checklist).

---

## 5. Roadmap gợi ý (rút gọn, theo phần còn giá trị)

1. Sửa README cho khớp `build-server.sh` / `run-server.sh`.
2. (Tuỳ chọn) WebSocket stats hoặc tối ưu polling.
3. Compose `-it` hoặc tích hợp terminal (phức tạp trên Windows/WSL).
4. Tính năng image nâng cao và diagnostics nếu cần.

---

## 6. Kết luận

DockLite đã vượt qua mô tả «prototype» trong nhiều điểm: DI, envelope API, Engine API, compose/service, batch, settings một lần load, dialog service, token shutdown trên luồng dài.

Tài liệu `docklite_updated_review.md` bản cũ giữ lại danh sách vấn đề như thể chưa sửa — **không còn phản ánh đúng**. Bản cập nhật này dùng làm baseline cho review tiếp theo.
