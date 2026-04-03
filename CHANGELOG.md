# Nhật ký thay đổi DockLite

Định dạng dựa trên [Keep a Changelog](https://keepachangelog.com/vi/1.0.0/).

## [Chưa phát hành]

### Thêm / đổi (i18n `StatusMessage` — các tab ngoài Cài đặt)

- `Resources/UiStrings.vi.xaml`, `UiStrings.en.xaml`: khóa `Ui_Status_Common_*`, `Ui_Compose_Status_*`, `Ui_Containers_Status_*`, `Ui_Containers_Batch_Status_*`, `Ui_Logs_Status_*`, `Ui_Images_Status_*`, `Ui_NetVol_Status_*`, `Ui_AppLog_Status_*` (và các chuỗi chung cho nhãn/lỗi).
- `UiLanguageManager`: `TryLocalizeCurrent`, `TryLocalizeFormat`, `TryLocalizeFormatCurrent` (định dạng theo `CultureInfo.CurrentUICulture`).
- ViewModel: `ComposeViewModel`, `ContainersViewModel` (+ `ContainersViewModel.BatchStats`), `LogsViewModel`, `ImagesViewModel`, `NetworkVolumeViewModel`, `AppDebugLogViewModel`, `CleanupViewModel`, `SettingsViewModel` — thay literal tiếng Việt trong `StatusMessage` bằng khóa trên khi phù hợp; giữ nguyên `ex.Message` / thông báo từ API.

### Thêm / đổi (tự khởi động WSL khi mở app)

- `WslDockerServiceAutoStart.TryEnsureRunningAsync`: khi bật tự khởi động và `/api/health` lỗi, gọi `bash scripts/restart-server.sh` thay cho `run-server.sh` (dừng tiến trình cũ rồi chạy lại). Thiếu file `restart-server.sh` trả `WslEnsureFailureReason.MissingRestartScript`.
- `ShellViewModel.ApplyWslStartupProgress`: chữ header phản ánh `restart-server.sh` và chờ health sau restart.
- Gợi ý khi health timeout sau spawn WSL: làm rõ đã gọi restart service.

### Tài liệu

- `README.md`: luồng mở app (tự khởi động) và mục tìm thư mục dịch vụ — đồng bộ với `restart-server.sh`; nút **Khởi động service WSL** trong Cài đặt vẫn dùng `run-server.sh`.
- `docs/docklite-optimization-and-extensions.md`: rà soát checklist — **[x] 1.2** (ListBox log + `VirtualizingStackPanel` đã có trong `LogsView` / `AppDebugLogView`), **[x] 2.1** (partial `ContainersViewModel.BatchStats.cs`), **[x] 5.5** (theme, `ServiceBaseUrlPortHint`, i18n vỏ + trợ giúp; backlog tùy chọn: i18n nội dung dòng log thô / API). Cập nhật mô tả §1.2, §2.1, §5.5.

### Thêm / đổi (i18n StatusMessage — Cài đặt)

- `SettingsViewModel`: `UiLanguageManager.TryLocalizeCurrent` / `TryLocalizeFormatCurrent` với khóa `Ui_Settings_Status_*` trong `UiStrings.vi/en.xaml` (kiểm tra nhanh, validation Lưu, tiến trình WSL, đồng bộ mã, kiểm tra kết nối, hủy khi đóng app). Chuỗi động từ `WslDockerServiceAutoStart` / ngoại lệ không ép dịch.

### Thêm / đổi (header: tiến trình chờ health WSL)

- `ShellViewModel`: `WslStartupProgressBarVisible` / `Indeterminate` / `Value` từ `ApplyWslStartupProgress`; `ClearWslStartupProgressBar` khi `RefreshServiceHeaderFromApiAsync`; toast Info một lần khi vào phase chờ health (sau spawn WSL).
- `MainWindow`: `ProgressBar` dưới hàng nút header, căn phải.
- `AppShellFactory`: truyền `INotificationService` vào `ShellViewModel`.

### Thêm / đổi (i18n vỏ cửa sổ vi/en)

- `AppSettings.UiLanguage` (vi/en), `AppSettingsDefaults.Normalize`.
- `Resources/UiStrings.vi.xaml`, `Resources/UiStrings.en.xaml`; `UiLanguageManager` gộp từ điển và đặt `CultureInfo` UI.
- `AppShellFactory`: gọi `ThemeManager` + `UiLanguageManager` sau khi load cài đặt (bỏ trùng `ThemeManager` trong `App.xaml.cs`).
- Cài đặt: ComboBox ngôn ngữ + tab **Kết nối**, **WSL và service**, **Hiển thị**, **Chờ và health**, hàng nút dưới cùng — `DynamicResource`; `RebuildUiThemeTitles` cho nhãn Sáng/Tối; `MainWindow` giữ như trước.
- Trợ giúp theo màn hình: `PageHelpTexts` đọc khóa `Ui_Help_*` trong `UiStrings.vi/en.xaml`; tiêu đề hộp thoại `Ui_Help_DialogTitlePrefix`; tiền tố xem trước ngày giờ `Ui_Settings_Display_DatePreviewPrefix`. `UiLanguageManager.TryLocalize` (fallback khi không có app/khóa).
- Nội dung nhật ký ứng dụng và một số thông báo từ API hoặc `ex.Message` có thể không qua từ điển UI; `StatusMessage` trên nhiều màn đã đọc `UiStrings` theo ngôn ngữ giao diện.

### Thêm / đổi (lazy tab VM, log ảo hóa, Compose terminal WSL, validate exec)

- `AppShellFactory` / `ShellViewModel`: khởi tạo lười (`Lazy<T>`) cho 7 ViewModel tab (Container, Log, Compose, Image, Mạng và volume, Dọn dẹp, Nhật ký ứng dụng); Tổng quan và Cài đặt tạo ngay; `OnCurrentPageChanged` dùng `IsValueCreated` để không tạo VM khi chưa vào tab.
- `LogsView` / `AppDebugLogView`: `VirtualizingStackPanel` tường minh với `CacheLength` (page) và `ScrollUnit="Pixel"` cùng recycling.
- Trang **Compose**: nút **Mở terminal WSL trong thư mục project** — `wsl.exe` (và `-d` distro từ Cài đặt nếu có) mở bash tại `WslPath` của project đã chọn.
- Service Go: `validateComposeServiceName` từ chối `..`; `parseExecCommandParts` từ chối thêm `> < ' "` trong từng đối số.

### Thêm / đổi (UI stats batch, test, README TLS, partial ViewModel)

- Trang **Container**: nút **Stats batch (chọn)** — `POST /api/containers/stats-batch` khi có ít nhất một ô đã chọn trên hai hàng (tối đa 32); kết quả tóm tắt CPU/RAM trong ô trạng thái. `ContainersViewModel.BatchStats.cs` (partial).
- `docs/docklite-optimization-and-extensions.md`: checklist 2.1, 2.2, mục 6 TLS.
- README: mục «TLS và truy cập ngoài máy cục bộ» — gợi ý HTTPS qua reverse proxy.
- `DockLite.Tests`: `Deserialize_success_envelope_stats_batch`.

### Thêm / đổi (API batch stats, timeout context, tab state, gợi ý cổng)

- Go: POST `/api/containers/stats-batch` (tối đa 32 id); `snapshotStatsForContainer` + `statsSnapshotFromStatsJSON` dùng chung cho GET stats đơn và batch.
- Go: `internal/httpserver/context_timeout.go` — `RequestContextTimeout`: deadline context cho inspect (2 phút), GET `/stats` (90 giây), POST stats-batch (3 phút); bỏ qua `/ws/*`.
- Contracts + `IDockLiteApiClient` + `DockLiteApiClient`: `ContainerStatsBatchRequest` / `ContainerStatsBatchData`, `GetContainerStatsBatchAsync`.
- `AppShellActivityState`: `SetComposePageVisible`, `SetImagesPageVisible`, `SetNetworkVolumePageVisible`; `ShellViewModel` cập nhật khi đổi tab.
- Cài đặt: khi Base URL dùng cổng mặc định 17890, `ServiceBaseUrlPortHint` gợi ý đổi `DOCKLITE_ADDR` và Base URL nếu cổng bị chiếm.

### Thêm / đổi (Compose — cache danh sách project trong RAM)

- Go `internal/compose`: bộ nhớ đệm có `sync.RWMutex` cho danh sách project sau lần đọc `compose_projects.json` đầu tiên; `saveProjects` cập nhật cache (sao chép sâu `ComposeFiles`); mọi `loadProjects` trả bản sao để caller vẫn sửa an toàn như trước.

### Thêm / đổi (Đồng bộ header và log HTTP service)

- `WslServiceHealthCache`: tham số `forceNotify` trên `SetFromHealthResponse` và `RefreshAsync` — khi «Kiểm tra kết nối», «Kiểm tra nhanh WSL» hoặc sau start/stop/restart service thủ công, vẫn làm mới dòng header dù trạng thái healthy (true/false) không đổi (ví dụ cập nhật phiên bản từ `/api/health`).
- `SettingsViewModel`: truyền `forceNotify: true` cho các lệnh trên.
- Service Go: `internal/httpserver/logging.go` — middleware `LogRequests` dùng `log/slog` với `req_id` (hex ngẫu nhiên) và thời gian xử lý (`http_request` / `http_request_done`); `cmd/server/main.go` ghi `docklite-wsl_listen` bằng slog.

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
- Trang **Container**: `GET /api/containers` trả `labels`; cột **Nhãn** (rút gọn) và ô **Tìm** khớp khóa/giá trị nhãn; panel **Gợi ý lệnh terminal** với `docker exec` / `docker attach` và nút sao chép.
- Service Go: `GET /api/images/{id}/inspect`, `GET /api/images/{id}/history`, `POST /api/images/pull`, `POST /api/images/load`, `GET /api/images/{id}/export` (tar); `GET /api/networks`, `GET /api/volumes`. Trang **Image**: Expander inspect/history/pull/export-import; trang **Mạng và volume** (sidebar).
- Trang **Image**: pull / xuất tar / nhập tar chạy trên thread pool (`Task.Run`) để không chặn UI; sau import gọi làm mới danh sách nhẹ (`ReloadImageListAsync`).
- Trang **Log**: giới hạn quét tiền tố cho phân loại mức và ô tìm; chu kỳ flush follow tự điều chỉnh theo backlog và thời gian tick (`LogsViewModel`, `LogLineClassifier`).
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

- `docker compose exec -it` / terminal tương tác nhúng trong UI (ConPTY); đã có nút mở terminal WSL trong thư mục project và khối gợi ý lệnh / sao chép.
- Rà thêm ViewModel hoặc lệnh chạy lâu khi bổ sung tính năng mới: gắn `IAppShutdownToken` / `CancellationToken` nhất quán.
- Tuỳ chọn: API batch stats một lần nhiều container; TLS khi expose service ra LAN/WAN.

### Tài liệu

- `docs/docklite-improvement-plan.md` và `docs/docklite-optimization-and-extensions.md`: cập nhật checklist khi đổi hạng mục backlog.

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
