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
- **Đã làm:** phân loại mức log (`LogLineClassifier`) và ô **Tìm** chỉ quét tiền tố dòng (4096 / 65536 ký tự); chu kỳ flush follow tự điều chỉnh theo độ đầy bộ đệm và thời gian xử lý một tick (heuristic thay cho đo FPS); khi không còn backlog nhỏ lại về 150 ms. **ListBox** log (container và nhật ký ứng dụng): `VirtualizingStackPanel` tường minh với `CacheLength` theo page, `ScrollUnit="Pixel"`, `VirtualizationMode="Recycling"` (`LogsView`, `AppDebugLogView`).
- **Tối ưu có thể làm thêm:**
  - Cân nhắc **ItemsRepeater** hoặc control ảo hóa tốt hơn nếu sau này cần hàng chục nghìn dòng thường xuyên (WPF `ListBox` đã ảo hóa; WinUI ItemsRepeater không áp dụng trực tiếp).

### 1.3 Dashboard và làm mới định kỳ

- **Mở rộng:** tần suất refresh có thể gắn với trạng thái kết nối (chỉ poll nhanh khi có lỗi; chậm lại khi ổn định).

### 1.4 Bộ nhớ và HttpClient

- **Hiện trạng:** `DockLiteHttpSession` quản lý vòng đời `HttpClient` khi đổi cài đặt.
- **Tối ưu:** đảm bảo mọi `IDisposable` từ response (stream) luôn được đóng trong `DockLiteApiClient` (rà soát từng endpoint có body lớn).

---

## 2. Kiến trúc client và khả năng kiểm thử

### 2.1 Kích thước ViewModel

- Một số ViewModel (ví dụ `ContainersViewModel`) có nhiều trách nhiệm: danh sách, batch, inspect, stats, top RAM/CPU.
- **Đã làm (partial):** `ContainersViewModel.BatchStats.cs` — UI và lệnh **Stats batch (chọn)** (`POST /api/containers/stats-batch`). Phần realtime một container, sparkline và top RAM/CPU vẫn trong `ContainersViewModel.cs` (đọc mã theo partial cùng lớp).
- **Hướng thêm:** trích **dịch vụ tầng trung gian** (ví dụ `IContainerStatsCoordinator` chỉ lo timer + hủy) nếu sau này cần test đơn vị sâu hơn — không bắt buộc ngay.

### 2.2 Unit test và contract

- `DockLite.Tests` hiện tập trung contract/health; có thể mở rộng:
  - Test **parse** `ApiResult` / lỗi envelope với JSON mẫu.
  - Test **format** chuỗi hiển thị (nếu tách pure function).

### 2.3 Navigation và vòng đời tab

- **Đã làm:** bảy tab phụ (Container, Log, Compose, Image, Mạng và volume, Dọn dẹp, Nhật ký ứng dụng) dùng `Lazy<T>` trong `AppShellFactory`; `ShellViewModel` expose qua property và `OnCurrentPageChanged` dùng `IsValueCreated` trước `ReferenceEquals` để không tạo VM khi chưa vào tab. **Tổng quan** và **Cài đặt** vẫn khởi tạo ngay.
- **Có thể làm thêm:** **giải phóng** tài nguyên khi rời tab (dừng timer, hủy subscription) nếu sau này cần giảm RAM cho session dài — hiện VM lazy vẫn sống sau lần tạo đầu tiên.

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
- Thao tác **attach** (hạn chế trên GUI): panel **Gợi ý lệnh terminal** với `docker exec -it … sh` và `docker attach …`, nút sao chép (`ContainersView`, `ContainersViewModel`).
- **Nhãn** (labels): API `GET /api/containers` trả `labels`; cột **Nhãn** và ô **Tìm** khớp key/value nhãn (`ContainerSummaryDto`, `MatchesSearch`).

### 5.2 Image

- **Inspect** JSON, **history** layer, **pull** (log stream rút gọn tối đa ~512 KiB trong phản hồi), **export/import** tar: `GET/POST` API trong `wsl-docker-service` (`image_detail.go`), khối Expander trên trang Image (`ImagesViewModel`, `ImagesView`).
- Client: pull / export / import chạy qua `Task.Run` để không khóa luồng giao diện trong lúc đọc/ghi file và HTTP dài.

### 5.3 Mạng và volume (mới)

- Liệt kê **network** và **volume**: `GET /api/networks`, `GET /api/volumes` (`networks.go`, `volumes.go`); trang **Mạng và volume** (`NetworkVolumeViewModel`, `NetworkVolumeView`).

### 5.4 Compose

- **Đã làm:** nút **Mở terminal WSL trong thư mục project** (`ComposeView`, `ComposeViewModel.OpenTerminalInProjectFolderCommand`): `wsl.exe` chạy `bash -lc` với `cd` tới `WslPath` của project đã chọn; thêm `-d` và tên distro khi Cài đặt có Distro WSL. Nút **Sao chép lệnh terminal** dán lệnh gợi ý `docker compose … exec -it … sh` (cùng file compose đã cấu hình).
- **Backlog:** shell **tương tác** (`-it`) **nhúng trong cửa sổ DockLite** (ConPTY / control tương đương Windows Terminal) — phức tạp; hiện dùng terminal WSL bên ngoài như trên.

### 5.5 Cài đặt và chủ đề

- **Đã làm:** theme sáng/tối (`ThemeManager`, ComboBox trong Cài đặt); gợi ý dưới ô base URL khi cổng trùng ứng dụng phổ biến (`ServiceBaseUrlPortHint`, `SettingsViewModel`); **ngôn ngữ UI** vi/en (`AppSettings.UiLanguage`, `UiStrings.*.xaml`, `UiLanguageManager`, vỏ `MainWindow`, Cài đặt, trợ giúp `Ui_Help_*`, tiền tố xem trước ngày giờ). **Trạng thái Cài đặt:** `SettingsViewModel` dùng khóa `Ui_Settings_Status_*` (`StatusLoc` / `StatusLocFormat`) cho thông báo cố định; chuỗi từ service WSL (`msg`) và `ex.Message` giữ nguyên ngôn ngữ nguồn. **Backlog tùy chọn:** i18n `StatusMessage` ở ViewModel khác và văn bản dòng log.

---

## 6. Bảo mật và thực hành tốt

- **exec compose** (`wsl-docker-service/internal/compose/compose_services.go`): `validateComposeServiceName` từ chối `..` và ký tự shell trong tên service; `parseExecCommandParts` tách bằng `strings.Fields` và từ chối từng đối số chứa bất kỳ ký tự nào trong mảng `forbidden` (trong mã nguồn), kèm giới hạn số đối số và độ dài — giảm rủi ro chèn shell khi ghép vào `docker compose exec -T`. Khi nới lỏng whitelist cần rà soát lại.
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
- `docs/docklite-analysis-and-roadmap-forward.md` — mục 5 (hướng phát triển tiếp theo; ví dụ 5.1 ngắn hạn).
- Mục 9 trong tài liệu này: checklist tiến độ tối ưu và mở rộng (chi tiết theo từng mục 1–6 và bảng mục 7).

---

## 9. Checklist tiến độ triển khai

Cách đọc:

- `[x]` đã triển khai trong codebase (đủ cho mục tiêu đề).
- `[~]` một phần: có phần liên quan nhưng chưa đủ phạm vi gốc trong tài liệu.
- `[ ]` chưa làm hoặc backlog.

Cập nhật checklist này khi hoàn thành thêm hạng mục (cùng thư mục `docs/` hoặc `CHANGELOG.md`).

### 1. Ứng dụng WPF

- [x] **1.1** Tạm dừng / giảm polling stats khi **không ở trang Container** hoặc **cửa sổ không active** / **thu nhỏ** (`AppShellActivityState`, `MainWindow`, `ShellViewModel`, `ContainersViewModel`).
- [x] **1.1** API batch stats + UI: POST `/api/containers/stats-batch`; nút **Stats batch (chọn)** trên trang Container (2–32 hàng đã tick); realtime một container vẫn GET/WebSocket như trước.
- [x] **1.1** Chỉ báo tải theo polling: **số lần gọi API stats** (realtime) trong phiên chọn container; chưa có «request/phút» toàn cục.
- [x] **1.2** Giới hạn quét phân loại / tìm trên tiền tố dòng dài (`LogLineClassifier`, `LogsViewModel.LineMatchesSearch`); không dùng regex trên toàn bộ dòng.
- [x] **1.2** Tạm dừng timer flush follow log khi **không ở tab Log** hoặc cửa sổ **không foreground** (`AppShellActivityState.ShouldProcessLogsFollowFlush`, `LogsViewModel`, `ShellViewModel.SetLogsPageVisible`).
- [x] **1.2** Giảm tần suất follow khi buffer/chunk pending lớn hoặc một tick xử lý lâu (`AdjustFollowFlushIntervalAfterWork`); hồi phục chu kỳ ngắn khi backlog giảm.
- [x] **1.2** WPF: `ListBox` + `VirtualizingStackPanel` tường minh (`CacheLength` theo page, `ScrollUnit="Pixel"`, `VirtualizationMode="Recycling"`) cho Log container và Nhật ký ứng dụng (`LogsView`, `AppDebugLogView`). ItemsRepeater (WinUI) không dùng; log cực lớn vẫn có thể đổi control sau này — xem §1.2.
- [x] **1.3** Dashboard: tần suất refresh gắn với trạng thái kết nối (poll nhanh khi lỗi, chậm khi ổn; `DashboardViewModel` + `AppShellActivityState.SetDashboardPageVisible`).
- [x] **1.4** Rà soát `DockLiteApiClient`: đã ghi chú XML; mọi `HttpResponseMessage` dùng `using`; body đọc buffer một lần trong `ReadEnvelopeAsync`.
- [x] **1.5** Dòng header «Trạng thái service (WSL)» đồng bộ sau «Kiểm tra kết nối», «Kiểm tra nhanh» và start/stop/restart WSL thủ công: `WslServiceHealthCache.SetFromHealthResponse` / `RefreshAsync` với `forceNotify` khi cần làm mới dù true/false không đổi (ví dụ phiên bản service).

### 2. Kiến trúc client và kiểm thử

- [x] **2.1** `ContainersViewModel` tách partial `ContainersViewModel.BatchStats.cs` (stats batch UI + lệnh); stats realtime/sparkline một container và top RAM/CPU vẫn trong `ContainersViewModel.cs` — đủ mục tiêu tách phần batch; xem §2.1.
- [x] **2.2** Unit test: envelope + deserialize `ContainerStatsBatchData` (`ApiEnvelopeJsonTests.Deserialize_success_envelope_stats_batch`); formatter inspect ở `DockLite.App` vẫn chưa test tự động.
- [x] **2.3** Lazy khởi tạo ViewModel cho 7 tab (Container, Log, Compose, Image, Mạng và volume, Dọn dẹp, Nhật ký ứng dụng) qua `Lazy<T>` trong `AppShellFactory` / `ShellViewModel`; **Tổng quan** và **Cài đặt** vẫn tạo ngay (header WSL cần snapshot từ Cài đặt). `OnCurrentPageChanged` dùng `IsValueCreated` để không kích hoạt lazy khi đang ở tab khác.

### 3. Service Go (`wsl-docker-service`)

- [x] **3.1** Một client Docker tái sử dụng (`dockerengine`); các handler gọi `dockerengine.Client()`, không tạo client mới mỗi request.
- [x] **3.1** Deadline `context` theo đường dẫn: `RequestContextTimeout` — inspect 2 phút, GET stats 90 giây, POST stats-batch 3 phút; WebSocket không bọc; `http.Server` vẫn giới hạn đọc/ghi dài cho compose/image.
- [x] **3.2** WebSocket stats cho **một** container (`/ws/containers/{id}/stats?intervalMs=`), sparkline CPU/RAM trong UI; polling REST vẫn tùy chọn khi tắt «Dùng WebSocket stream».
- [x] **3.3** Cache danh sách project trong RAM (`internal/compose/compose_cache.go` + cập nhật khi `saveProjects`; không theo dõi sửa file ngoài tiến trình — một process duy nhất).
- [x] **3.3** Tùy chọn compose file `-f` (nhiều file): lưu trong project service (`composeFiles`), PATCH `/api/compose/projects/{id}`, UI Compose (thêm project + chỉnh sửa + Lưu).
- [x] **3.4** `GET /api/metrics` nhẹ (text/plain, bộ đếm request HTTP; middleware tăng đếm).
- [x] **3.4** Structured logging phía Go: `log/slog` trong middleware `LogRequests` (`req_id`, `http_request` / `http_request_done`, `ms`).

### 4. Tích hợp WSL và độ tin cậy

- [x] **4.1** Backoff sau khi spawn WSL (tăng khoảng chờ giữa các lần thử health; tối đa 5 s mỗi vòng).
- [x] **4.1** UI tiến trình / thời gian chờ health: header có `ProgressBar` (indeterminate khi kiểm tra/gửi WSL; determinate theo thời gian đã trôi / timeout khi chờ `/api/health`); dòng phụ `Còn ~Xs / Ys`; toast thông tin một lần khi bắt đầu vòng chờ health; toast cảnh báo khi health timeout (giữ như trước).
- [x] **4.2** README đồng bộ script: `build-server.sh` (build) và `run-server.sh` (chỉ chạy binary), tránh nhầm lỗi `go build` khi chỉ chạy script run.

### 5. Mở rộng theo module

- [x] **5.1** Inspect: tóm tắt (`ContainerInspectSummaryFormatter`) + **bảng** mount, cổng publish, mạng, env, nhãn (`ContainerInspectGridParser`, `ContainersView`); lọc nhãn trong khối chi tiết (ô lọc).
- [x] **5.1** Attach / gợi ý lệnh terminal ngoài (`docker exec` / `docker attach`, sao chép trong panel chi tiết).
- [x] **5.1** Lọc **danh sách container** theo label: trường `labels` từ API + ô Tìm trên key/value; cột Nhãn rút gọn; lọc nhãn trong bảng inspect sau «Tải chi tiết» vẫn giữ nguyên.
- [x] **5.2** Inspect image, history layer, pull (log rút gọn trong envelope) + export/import tar (đồng bộ; UI có thanh trạng thái).
- [x] **5.2** Export/import/pull image: thao tác mạng và file chạy qua `Task.Run` trong `ImagesViewModel` để không chặn luồng UI; làm mới danh sách sau import/pull vẫn trên UI (HTTP ngắn).
- [x] **5.3** Trang và API liệt kê network, volume (Go + Docker API).
- [x] **5.4** Compose: nút **Mở terminal WSL trong thư mục project** (`ComposeViewModel.OpenTerminalInProjectFolderCommand`, `wsl.exe` + bash tại `WslPath`, distro tùy chọn từ Cài đặt) và **Sao chép lệnh terminal** (`SuggestedWslTerminalComposeExecLine`). Shell tương tác **nhúng** trong UI (ConPTY) vẫn backlog — xem §5.4.
- [x] **5.5** Theme sáng/tối; gợi ý cổng khi trùng (`ServiceBaseUrlPortHint`). **Ngôn ngữ UI:** `AppSettings.UiLanguage` (vi/en), `UiStrings.*.xaml`, `UiLanguageManager`, ComboBox **Hiển thị**; chuỗi đã gắn: vỏ `MainWindow`, trang **Cài đặt** (bốn tab + hàng nút), nhãn chủ đề (`RebuildUiThemeTitles`), trợ giúp (`Ui_Help_*`), tiền tố xem trước ngày giờ. **Backlog tùy chọn:** i18n toàn bộ `StatusMessage` và nội dung dòng log — xem §5.5.

### 6. Bảo mật và thực hành tốt

- [x] **6** Exec compose: `compose_services.go` — `validateComposeServiceName` (tên service: không `..`, không shell metachar theo hàm) và `parseExecCommandParts` (đối số: danh sách `forbidden` gồm `> < ' "` và các ký tự điều hướng shell). Khi mở rộng whitelist cần rà soát lại (không thay cho đánh giá bảo mật đầy đủ).
- [x] **6** Cảnh báo Base URL không phải localhost (rủi ro MITM LAN) — ô gợi ý dưới URL trong Cài đặt.
- [x] **6** TLS / reverse proxy: README mục «TLS và truy cập ngoài máy cục bộ» — gợi ý HTTPS qua reverse proxy; không TLS trong binary.

### Roadmap mục 5.1 (`docklite-analysis-and-roadmap-forward.md`)

- [x] **5.1** Tối ưu theo **tab và trạng thái cửa sổ** (poll Container/Dashboard; timer follow log khi tab Log + foreground) — cùng hàng với mục **1.1** và dòng **1.2** (timer follow) ở trên.
- [x] **5.1** Rà soát `DockLiteApiClient` — cùng mục **1.4** (dispose response, buffer envelope).
- [x] **5.1** Luồng **kiểm tra nhanh** WSL: health + Docker + wslpath các ô (tab WSL, `QuickWslDiagnosticsCommand`; gom thao tác cho người mới).

### Roadmap mục 5.2 (`docklite-analysis-and-roadmap-forward.md`)

- [x] **5.2** Inspect chi tiết: **bảng** mount, cổng, env, nhãn (và mạng); lọc nhãn trong panel chi tiết — cùng mục **5.1** checklist (inspect bảng).
- [x] **5.2** Compose: tham số **`-f`** / nhiều file compose (`wsl-docker-service` `composeFiles`, PATCH, `ComposeViewModel`).
- [x] **5.2** Stats: sparkline + WebSocket stream (mục **3.2**); biểu đồ đầy đủ / SSE là backlog tùy chọn.

### 7. Bảng ưu tiên gợi ý (đối chiếu)

| Ưu tiên | Hạng mục | Trạng thái | Ghi chú |
| ------- | -------- | ---------- | ------- |
| Cao | Đồng bộ README với script build/run | Đã | README và mục **4.2** checklist. |
| Cao | Tạm dừng/giảm polling khi tab ẩn hoặc app không foreground / thu nhỏ | Đã | Mục **1.1** checklist đầu tiên. |
| Trung bình | WebSocket stats cho một container đang xem | Đã | Mục **3.2**; sparkline + tùy chọn polling. |
| Trung bình | Parse inspect JSON thành bảng | Đã | Mục **5.1** / **5.2**; bảng mount, cổng, mạng, env, nhãn + lọc nhãn trong chi tiết. |
| Thấp | Networks/volumes, image pull/export | Đã | Mục **5.2**, **5.3**; export/import/pull không chặn UI (xem checklist **5.2** job nền). |

