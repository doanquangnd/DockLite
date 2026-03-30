# Nhật ký thay đổi DockLite

Định dạng dựa trên [Keep a Changelog](https://keepachangelog.com/vi/1.0.0/).

## [Chưa phát hành]

### Thêm / đổi (Đồng bộ mã nguồn Go vào WSL)

- `AppSettings`: `WslDockerServiceLinuxSyncPath` (đích Unix trong WSL), `WslDockerServiceSyncSourceWindowsPath` (nguồn Windows riêng cho đồng bộ; để trống = cùng thư mục với ô dịch vụ), `WslDockerServiceSyncDeleteExtra` (rsync `--delete` khi có rsync), `WslDockerServiceSyncEnforceVersionGe` (chỉ đồng bộ khi `VERSION` nguồn >= đích).
- `wsl-docker-service/internal/appversion/VERSION` + Go `internal/appversion` (embed); `/api/health` lấy version từ file này.
- `DockLite.Core.DockLiteSourceVersion`: đọc/so sánh `System.Version` từ file VERSION.
- Cài đặt, tab **WSL và service**: ô đích Unix, tùy chọn xóa file thừa, tùy chọn kiểm tra version, nút **Đồng bộ mã nguồn vào WSL**; hàng nút dưới cùng **Đồng bộ mã → WSL**.
- `WslDockerServiceAutoStart.TrySyncWindowsSourceToLinuxDestinationAsync`: tùy chọn đọc VERSION trên WSL trước khi `rsync`/`cp`; gọi `wsl` + `bash -lc` cho đồng bộ.

### Thêm / đổi (Tối ưu theo `docs/docklite-optimization-and-extensions.md`)

- `AppShellActivityState` (singleton DI): biết cửa sổ chính có đang foreground và không thu nhỏ hay không, và sidebar có đang mở trang **Container** hay không. `MainWindow` cập nhật khi `Activated` / `Deactivated` / `StateChanged`; `ShellViewModel` cập nhật khi `CurrentPage` đổi. `ContainersViewModel` chỉ chạy timer **stats realtime** (polling API) khi cả hai điều kiện cho phép, để giảm tải khi chuyển tab hoặc sang app khác.
- Trang **Tổng quan**: làm mới định kỳ khi tab đang mở và cửa sổ tương tác — chu kỳ ngắn hơn khi lần làm mới trước lỗi, dài hơn khi ổn định (`DashboardViewModel` + `DispatcherTimer`).
- Trang **Container**: bộ đếm số lần gọi API stats (realtime); khối **Inspect (tóm tắt)** (`ContainerInspectSummaryFormatter`) đọc các trường thường gặp từ JSON inspect; khối JSON thô và Stats giữ như trước.
- **Cài đặt**: cảnh báo khi base URL có host không phải localhost (HTTP trên LAN).
- `WslDockerServiceAutoStart`: backoff khoảng chờ giữa các lần thử `/api/health` sau khi spawn WSL (thay vì cố định từng vòng).
- Service Go: `GET /api/metrics` (text/plain, bộ đếm request HTTP đơn giản); middleware tăng bộ đếm.
- `DockLiteApiClient`: ghi chú XML về `using` response và đọc body một lần.
- `DockLite.Tests`: test deserialize `ApiEnvelope` JSON thành công / lỗi.
- README: làm rõ `scripts/build-server.sh` (build Go) so với `scripts/run-server.sh` (chỉ chạy binary); gỡ lỗi `go mod` / `go build` gắn với bước build, không phải với `run-server.sh` khi đã có `bin/docklite-wsl`; thêm `GET /api/metrics`.

### Thêm / đổi (Hướng mở rộng gần đây — stats, compose exec, settings, log UI)

- `WslDistroProbe`: **Thử distro** (`uname -a` qua `wsl.exe`). Cài đặt: **Cập nhật tóm tắt** đường dẫn WSL (distro, thư mục service, base URL).
- Go: `GET /api/containers/top-by-memory`, `GET /api/containers/top-by-cpu`; snapshot stats có tổng **block read/write** (blkio). Trang **Container**: Top 5 RAM, Top 5 CPU, dòng ổ trong tóm tắt stats.
- Go: `POST /api/compose/service/exec` (`docker compose exec -T`, validate lệnh). Trang **Compose**: ô lệnh + **Chạy exec**.
- `IAppShutdownToken` truyền vào các ViewModel gọi API hoặc WebSocket dài (Container, Compose, Image, Cleanup, Logs; follow log dùng token liên kết).
- `LogsView`: `ListBox` cuộn ngang, dòng log `TextWrapping="NoWrap"` để ảo hóa ổn định hơn.

### Thêm / đổi (Toast nền mục 6 + log viewer mục 7)

- `NotificationDisplayKind`, `INotificationService.ShowAsync`: toast cảnh báo / thành công (màu khác nhau). `DashboardViewModel`: thông báo khi mất kết nối service/Docker (chỉ khi chuyển từ ổn sang lỗi hoặc lần đầu lỗi) và khi phục hồi. `ComposeViewModel` / `ImagesViewModel`: compose lỗi và prune image (thành công / thất bại).
- `AppDebugLogViewModel` / `AppDebugLogView`: lọc category và mức (heuristic), `VisibleLines` + ListBox ảo hóa; xuất log theo bộ lọc hoặc toàn bộ đuôi; sao chép chẩn đoán kèm phiên bản và OS.

### Thêm / đổi (Hướng mở rộng — container inspect + snapshot stats)

- Go: `GET /api/containers/{id}/inspect`, `GET /api/containers/{id}/stats` (một lần chụp qua `ContainerInspectWithRaw` / `ContainerStatsOneShot`).
- Contracts: `ContainerInspectData`, `ContainerStatsSnapshotData`; `IDockLiteApiClient` + `DockLiteApiClient`.
- `ContainersViewModel` / `ContainersView`: Expander chi tiết, nút **Tải chi tiết**; `docs/docklite-improvement-plan.md` cập nhật tiến độ mục 1–2.

### Thêm / đổi (Stats realtime — polling WPF)

- `ContainersViewModel`: `DispatcherTimer` + `SemaphoreSlim` (tránh chồng request), checkbox và ComboBox chu kỳ; `docs/docklite-improvement-plan.md` mục 2.

### Thêm / đổi (Batch — mục 4; Settings wslpath + Docker info — mục 5)

- `SelectableContainerRow` / `SelectableImageRow`: ô chọn trong grid; batch start/stop/xóa container, xóa nhiều image; `WslPathProbe` (`Infrastructure/wsl`) cho `ProbeWslpath` trong Cài đặt; kết nối kiểm tra hiển thị phiên bản Docker Engine.

### Thêm / đổi (Compose — service theo mục 3 kế hoạch)

- Go `internal/compose`: `compose_services.go` — `config --services`, `compose start|stop <service>`, `compose logs --tail`.
- Contracts + `DockLiteApiClient`: `ComposeServiceListData`, `ComposeServiceRequest`, `ComposeServiceLogsRequest`.
- `ComposeViewModel` / `ComposeView`: danh sách service, start/stop/logs, ghi chú shell chưa hỗ trợ.

### Thêm / đổi (Go `wsl-docker-service` — Engine API, mục 10 kế hoạch)

- `internal/dockerengine`: client Docker API (`client.NewClientWithOpts`), `WriteError` ánh xạ `errdefs` → HTTP; handlers `internal/docker` và `internal/ws` không còn `exec docker` cho container/image/info/prune/logs (REST + WebSocket).
- `internal/compose`: giữ `exec docker compose` cho `up`/`down`/`ps`; gói `internal/dockercli` (chỉ `WriteDockerCliError`, không còn được import) đã gỡ.

### Thêm / đổi (WSL stdout/stderr + gợi ý health — mục 4)

- `WslDockerServiceAutoStart`: vòng đệm tối đa 48 dòng stdout/stderr sau mỗi lần spawn; lưu dòng lệnh `wsl.exe … bash -lc …` và distro/path; `FormatHealthTimeoutUserHint` cho toast và Cài đặt; khi health timeout ghi block gợi ý vào log (`AppFileLog.WriteMultiline`).
- `AppFileLog.WriteMultiline`: ghi nhiều dòng cùng category.
- Toast (`WpfToastNotificationService`): `ScrollViewer` cho nội dung dài, thời gian hiển thị phụ thuộc độ dài.

### Thêm / đổi (wiring `MainWindow` / mục 1 kế hoạch)

- `IAppShellFactory`, `AddDockLiteUi`, `AppHostContext`: `MainWindow` chỉ nhận `ShellViewModel` + `IAppStartupService` + `IAppShutdownToken`; `IAppStartupService.RunInitialLoadAsync()` không truyền composition (DI inject `ShellCompositionResult` + `AppHostContext` vào `AppStartupCoordinator`).

### Thêm / đổi (theo `docs/docklite-improvement-plan.md`, mức 1)

- Script WSL: `build-server.sh` (chỉ build), `run-server.sh` (chỉ chạy `bin/docklite-wsl`, không `go mod tidy` mỗi lần), `restart-server.sh` (cố gắng `pkill` rồi chạy lại).
- `Build-GoInWsl.ps1` gọi `bash scripts/build-server.sh` qua `bash -lc`.
- `IDialogService` / `WpfDialogService`: Container, Image, Cleanup ViewModel không gọi `MessageBox` trực tiếp.
- `LogsViewModel`: gom chunk WebSocket theo `DispatcherTimer` (~150 ms), `BeginInvoke` cho thông báo; khi không có từ khóa tìm, cập nhật `Lines` theo dòng (tránh rebuild toàn bộ mỗi chunk).
- `WslDockerServiceAutoStart`: ghi log khi spawn `wsl`; redirect stdout/stderr UTF-8, `BeginOutputReadLine` / `BeginErrorReadLine`, ghi từng dòng vào `AppFileLog` (mục WSL stdout/stderr); giữ tham chiếu `Process` để đọc bất đồng bộ ổn định; khi hết thời gian chờ health sau tự khởi động, ghi dòng gợi ý xem file log.
- `AppShellFactory` + `ShellCompositionResult`: tách wiring khỏi `MainWindow`, một lần `store.Load()`; `SettingsViewModel` nhận `AppSettings` đã load (không đọc store lần hai lúc khởi tạo).
- `Microsoft.Extensions.DependencyInjection`: `AddDockLiteUi` đăng ký `AppHostContext`, `IAppShellFactory`, `ShellCompositionResult`, `ShellViewModel`, `IAppStartupService`, `INotificationService`, `MainWindow`; `App.xaml` bỏ `StartupUri`, `OnStartup` tạo `ServiceProvider` và `Show()` cửa sổ chính; `OnExit` dispose container.
- `IAppStartupService` / `AppStartupCoordinator`: gom `TryEnsureRunningAsync` + refresh dashboard sau `Loaded`.
- `INotificationService` / `WpfToastNotificationService`: toast góc màn hình (không modal, `ShowActivated = false`); timeout health WSL hiển thị bằng toast thay cho hộp thoại chặn.
- `MainWindow`: nhận `ShellCompositionResult` và `IAppStartupService` qua constructor (resolve từ DI).
- `DockLiteApiClient`: hầu hết endpoint dùng envelope JSON `{ success, data, error }` (`ApiEnvelope<T>` → `ApiResult<T>`); `ApiErrorBody` có `code` (domain), `message`, `details` (tuỳ chọn, output lệnh); `GetHealthAsync` vẫn GET `/api/health` không envelope (JSON trực tiếp).
- Service Go (`wsl-docker-service`): HTTP status có nghĩa hơn (ví dụ 503 khi `docker`/`docker info` lỗi, 404 khi không có container/project, 400/409 khi compose thêm trùng hoặc thư mục/file compose không hợp lệ, 500 khi lệnh compose/docker prune thất bại); POST compose thêm: kiểm tra thư mục tồn tại, có file compose, chặn trùng đường dẫn chuẩn hóa.
- Trang Compose: nút **Chọn thư mục** (`OpenFolderDialog`) điền đường dẫn Windows.
- `internal/apiresponse` (Go): `WriteSuccess` / `WriteError` / `WriteErrorWithDetails`; mã domain (`VALIDATION`, `NOT_FOUND`, `DOCKER_UNAVAILABLE`, …). REST `/api/*` (trừ `/api/health`) trả envelope; ViewModel đọc `ApiResult<T>`.
- `IAppShutdownToken` / `AppShutdownToken`: token hủy khi đóng cửa sổ chính; đăng ký singleton trong DI; `MainWindow` gọi `Cancel()` khi `Closed`.
- `AppStartupCoordinator`: `TryEnsureRunningAsync` dùng token liên kết với shutdown (không hiện dialog timeout / không refresh dashboard khi người dùng đóng app giữa lúc chờ).
- `SettingsViewModel`: truyền token shutdown vào chờ health / khởi động WSL thủ công / kiểm tra kết nối; bắt `OperationCanceledException` trước `Exception` trong luồng async.
- Trang **Nhật ký ứng dụng**: nút **Sao chép chẩn đoán** (clipboard: thư mục log, UTC, nội dung log) và **Xuất log ra file** (UTF-8 qua hộp thoại lưu file).

### Chưa làm (hẹn tiếp)

- Stream stats chuyên biệt qua WebSocket (thay hoặc bổ sung cho polling hiện tại).
- `docker compose exec -it` / shell tương tác trong UI (hiện ghi chú dùng terminal ngoài).
- Rà thêm các ViewModel hoặc lệnh chạy lâu nếu sau này bổ sung mà chưa gắn `IAppShutdownToken`.

### Sửa (kỹ thuật)

- `ShellViewModel` / `WslServiceHealthCache`: đồng bộ dòng trạng thái service trên header với cache health (khi `LastHealthy` đổi, ví dụ sau «Kiểm tra kết nối» hoặc khi `SetFromHealthResponse` từ Cài đặt); tránh trường hợp header vẫn hiển thị trạng thái ổn trong khi không kết nối được tới service WSL. `WslServiceHealthCache` chỉ raise `Changed` khi giá trị healthy thực sự thay đổi; `RefreshServiceHeaderFromApiAsync` bọc cập nhật cache bằng cờ nội bộ để không kích hoạt handler lặp vô hạn.
- `LogsViewModel`: thêm `using DockLite.App.Services` để resolve `IAppShutdownToken` sau khi inject token shutdown.

### Tài liệu

- `docs/docklite-improvement-plan.md`: đồng bộ checklist (stats top CPU/RAM, blkio, token shutdown trên nhiều ViewModel).

### Bổ sung (tiếp theo kế hoạch)

- Service Go `wsl-docker-service`: phân tách theo `docs/docklite-improvement-plan.md` mức 3 — `internal/httpserver`, `internal/docker`, `internal/dockerengine`, `internal/compose`, `internal/ws`, `internal/settings`; `cmd/server/main.go` gọn; xóa các file `compose.go` / `containers.go` / … cũ trong `cmd/server`.
- `WslDockerServiceAutoStart.TryEnsureRunningAsync`: trả về `(bool, WslEnsureFailureReason)`; health trước khi spawn WSL: `IsHealthOkWithRetryAsync` (3 lần, cách 250 ms); sau khi spawn: mỗi vòng chờ chỉ `IsHealthOkOnceAsync`.
- `AppStartupCoordinator`: nếu `HealthTimeoutAfterWslStart`, `INotificationService.ShowInfoAsync` gợi ý xem file log (đường dẫn thư mục).
- `IDialogService.ShowInfoAsync`; `WpfDialogService` triển khai; DI đăng ký `IDialogService` singleton, `AppShellFactory` nhận `IDialogService`.
- Trang **Nhật ký ứng dụng**: virtualized viewer; lọc theo category/level (bổ sung cho lọc dòng hiện có).

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
