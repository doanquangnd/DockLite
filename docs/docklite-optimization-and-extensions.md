# Phân tích tối ưu và mở rộng DockLite

Tài liệu mô tả các hướng **cải thiện hiệu năng**, **kiến trúc**, **service Go**, **tích hợp WSL** và **tính năng mới** phù hợp với codebase hiện tại. Bổ sung cho `docklite-improvement-plan.md` và `docklite_updated_review.md`.

---

## 1. Ứng dụng WPF (hiệu năng giao diện)

### 1.1 Trang Container và polling stats

- **Hiện trạng:** `DispatcherTimer` + `SemaphoreSlim` tránh chồng request; người dùng chọn chu kỳ (1–10 giây).
- **Tối ưu có thể làm:**
  - Tự động **giảm tần suất** khi cửa sổ không active (hoặc tạm dừng khi chuyển tab khác) để giảm tải CPU và mạng.
  - Gộp **một lần gọi** nếu sau này API hỗ trợ batch stats (hiện mỗi container một snapshot hoặc theo thiết kế API).
  - Hiển thị **số request/phút** hoặc chỉ báo «đang tải» để người dùng biết tác động của chu kỳ ngắn.

### 1.2 LogsViewModel và luồng log lớn

- **Hiện trạng:** batch chunk theo timer, `BeginInvoke`, tối ưu khi không tìm kiếm, ListBox ảo hóa, dòng không wrap.
- **Tối ưu có thể làm:**
  - Giới hạn **độ sâu** hoặc **số ký tự** mỗi dòng khi highlight (tránh regex nặng trên chuỗi rất dài).
  - Tùy chọn **giảm tần suất** follow khi FPS thấp hoặc buffer vượt ngưỡng.
  - Cân nhắc **ItemsRepeater** hoặc control ảo hóa tốt hơn nếu sau này cần hàng chục nghìn dòng thường xuyên.

### 1.3 Dashboard và làm mới định kỳ

- **Mở rộng:** tần suất refresh có thể gắn với trạng thái kết nối (chỉ poll nhanh khi có lỗi; chậm lại khi ổn định).

### 1.4 Bộ nhớ và HttpClient

- **Hiện trạng:** `DockLiteHttpSession` quản lý vòng đời `HttpClient` khi đổi cài đặt.
- **Tối ưu:** đảm bảo mọi `IDisposable` từ response (stream) luôn được đóng trong `DockLiteApiClient` (rà soát từng endpoint có body lớn).

---

## 2. Kiến trúc client và khả năng kiểm thử

### 2.1 Kích thước ViewModel

- Một số ViewModel (ví dụ `ContainersViewModel`) có nhiều trách nhiệm: danh sách, batch, inspect, stats, top RAM/CPU.
- **Hướng tách:** trích **dịch vụ tầng trung gian** (ví dụ `IContainerStatsCoordinator` chỉ lo timer + hủy) hoặc partial class chỉ để đọc mã; không bắt buộc đổi ngay nếu chưa có nhu cầu test đơn vị.

### 2.2 Unit test và contract

- `DockLite.Tests` hiện tập trung contract/health; có thể mở rộng:
  - Test **parse** `ApiResult` / lỗi envelope với JSON mẫu.
  - Test **format** chuỗi hiển thị (nếu tách pure function).

### 2.3 Navigation và vòng đời tab

- Mỗi tab giữ ViewModel sống trong `ShellViewModel`; khi tính năng tăng, có thể **lazy load** tab hoặc **giải phóng** tài nguyên khi rời tab (timer, subscription).

---

## 3. Service Go (`wsl-docker-service`)

### 3.1 Hiệu năng và Docker API

- **Tối ưu:** tái sử dụng **một client** Docker (đã có `dockerengine`); tránh tạo client mới mỗi request nếu chỗ nào còn tạo tạm.
- **Timeout:** cấu hình timeout HTTP phía Go (và giới hạn body) cho từng loại handler (stats, inspect JSON lớn).

### 3.2 Stats realtime

- **Hiện trạng:** snapshot REST; log qua WebSocket.
- **Mở rộng:** endpoint WebSocket hoặc SSE chỉ cho **một container** đang xem chi tiết, để giảm polling trùng trên nhiều client.

### 3.3 Compose

- **Hiện trạng:** vẫn `exec docker compose` cho up/down/ps và service helpers.
- **Tối ưu:** cache **danh sách project** hoặc path đã resolve (cẩn thận invalidation khi người dùng đổi file ngoài app).
- **Mở rộng:** tùy chọn **compose file** ( `-f` ) nếu người dùng có nhiều file trong thư mục.

### 3.4 Quan sát và vận hành

- **Metrics nhẹ:** Prometheus endpoint hoặc `GET /api/metrics` đơn giản (số request, lỗi Docker) — tùy nhu cầu.
- **Structured logging** phía Go (level, request id) để khớp với log app Windows khi gỡ lỗi.

---

## 4. Tích hợp WSL và độ tin cậy

### 4.1 Auto-start và health

- **Tối ưu:** backoff **sau khi spawn** WSL (thay vì cố định 500 ms mỗi vòng) để giảm CPU khi service chưa sẵn sàng.
- **Mở rộng:** hiển thị **số giây còn lại** hoặc progress trong UI khi chờ health.

### 4.2 README và script

- Đồng bộ mô tả: `build-server.sh` (build) và `run-server.sh` (chỉ chạy binary), tránh gợi ý lỗi `go build` khi chạy nhầm script.

---

## 5. Mở rộng tính năng theo module

### 5.1 Container

- Bảng **chi tiết inspect** (parse JSON) thay vì chỉ raw: mount, env, port, network dạng bảng.
- Thao tác **attach** (hạn chế trên GUI; thường mở terminal ngoài với lệnh gợi ý).
- **Nhãn** (labels) và filter theo label.

### 5.2 Image

- **Inspect** image, **history** layer, **pull** có tiến trình (nếu API và UI có chỗ hiển thị).
- **Export/import** (`docker save`/`load`) qua job nền + thông báo.

### 5.3 Mạng và volume (mới)

- Liệt kê **network**, **volume** (cần endpoint Go + Docker API); phù hợp quản trị nhẹ.

### 5.4 Compose

- Shell **tương tác** (`-it`): tích hợp terminal nhúng (Windows Terminal, ConPTY) — phức tạp, nên làm sau nếu cần.

### 5.5 Cài đặt và chủ đề

- **Theme** sáng/tối, **ngôn ngữ** (resource file), **cấu hình cổng** service mặc định nếu trùng ứng dụng khác.

---

## 6. Bảo mật và thực hành tốt

- **exec compose:** giữ validate lệnh (chặn shell injection); rà soát whitelist ký tự nếu nới lỏng.
- **Base URL:** cảnh báo nếu người dùng trỏ tới host không phải localhost (rủi ro MITM trong mạng LAN — tùy mô hình).
- **TLS** nếu sau này expose service ngoài máy: chứng chỉ, reverse proxy.

---

## 7. Ưu tiên gợi ý (không bắt buộc đúng thứ tự)


| Ưu tiên    | Hạng mục                                           | Lý do ngắn                       |
| ---------- | -------------------------------------------------- | -------------------------------- |
| Cao        | Đồng bộ README với script build/run                | Giảm gỡ lỗi nhầm cho người mới   |
| Cao        | Tạm dừng/giảm polling khi tab ẩn hoặc app minimize | Tránh tải không cần              |
| Trung bình | WebSocket stats cho một container đang xem         | Giảm polling, tăng độ “realtime” |
| Trung bình | Parse inspect JSON thành bảng                      | UX tốt hơn raw JSON              |
| Thấp       | Networks/volumes, image pull/export                | Giá trị thêm cho power user      |


---

## 8. Liên kết tài liệu

- `docs/docklite-improvement-plan.md` — checklist trạng thái đã làm / một phần.
- `docs/docklite_updated_review.md` — đối chiếu đánh giá cũ với code hiện tại.
- `CHANGELOG.md` — nhật ký thay đổi theo phiên bản.

