# Nhật ký thay đổi DockLite

Định dạng dựa trên [Keep a Changelog](https://keepachangelog.com/vi/1.0.0/).

## [2.0.0] — 2026-04-25 (phát hành nội bộ)

Milestone **v2.0 IPC Hardening (Zero-Trust)**: 5 phase, 27 yêu cầu trong `REQUIREMENTS.md`; self-attest trong `.planning/SECURITY-ATTESTATION.md`.

### Bảo mật và vận hành

- **Mạng (Go):** mặc định lắng nghe loopback; chế độ LAN fail-closed khi thiếu token; giới hạn tốc độ theo IP; timeout HTTP chuẩn + nới deadline cho luồng dài; audit JSON (stdout + file xoay).
- **Bí mật (WPF + Go):** token API trong Windows Credential Manager; xoay token qua `POST /api/auth/rotate` và nút Cài đặt.
- **TLS tùy chọn (Go + WPF):** khi `DOCKLITE_TLS_ENABLED=true`, service tự sinh cert ECDSA trong `~/.docklite/tls/`; client HTTPS/WSS với TOFU và pin fingerprint trong Credential Manager; UI Cài đặt (bật https, probe, quên pin).
- **Process hardening:** spawn lifecycle WSL qua `wsl.exe --cd <unixPath> -- bash <script>` (không nội suy `bash -lc` cho đường dẫn dự án); kiểm tra ký tự cấm trên đường dẫn Unix; script `run/stop/restart` dùng PID file `~/.docklite/run/docklite-wsl.pid` thay `pkill -f`; `--` trước tham số user-controlled cho `trivy` và `docker compose` (service/exec/logs).

### Kiểm thử

- `dotnet test` (DockLite.Tests).
- `go vet ./...` và `go test ./...` trong `wsl-docker-service` (môi trường có Go).

Các mục dưới **[Chưa phát hành]** vẫn là nhật ký chi tiết theo từng hạng mục phát triển trước khi gắn nhãn 2.0.0.

## [Chưa phát hành]

### Thêm / đổi (Quan sát — mục 2.5 tài liệu mở rộng)

- **`AppSettings`:** `ContainerStatsCpuWarnPercent` / `ContainerStatsMemoryWarnPercent` (0–100, 0 = tắt); `AppSettingsDefaults` chuẩn hóa ngoài biên về 0.
- **Cài đặt (tab Hiển thị):** hai ô ngưỡng; validate khi Lưu.
- **Màn Container:** `ContainersViewModel` đọc ngưỡng từ `IAppSettingsStore`; khối cảnh báo phía trên sparkline khi realtime vượt ngưỡng; `ShellViewModel` đồng bộ khi chuyển tab; trợ giúp / `Ui_Help_Containers_Body` cập nhật.

### Thêm / đổi (Image và registry — mục 2.4 tài liệu mở rộng)

- **Service Go:** `GET /api/images` — trường `sizeBytes` (byte) mỗi dòng; `POST /api/images/pull/stream` — luồng thô từ `ImagePull` (không envelope JSON); OpenAPI và timeout 30 phút giống pull buffer.
- **Client .NET:** `ImageSummaryDto.SizeBytes`; `IDockLiteApiClient.PullImageStreamAsync`; màn Image: khối tóm tắt sau lọc + gợi ý dangling/prune; xác nhận trước Prune dangling; pull ưu tiên stream, fallback pull buffer; ProgressBar khi pull; chuỗi `Ui_Images_Summary_*`, `Ui_Images_Prune*`, `Ui_Images_PullProgressStarting`.

### Thêm / đổi (Bảo mật và vận hành — mục 2.6 tài liệu mở rộng)

- **`docs/docklite-lan-security.md`:** hướng dẫn rủi ro HTTP không TLS trên LAN và token `DOCKLITE_API_TOKEN`.
- **Cài đặt (tab Kết nối):** banner nhắc HTTP/token; nút mở tệp Markdown khi `docklite-lan-security.md` phân giải được từ thư mục chạy (`LanSecurityDocPaths`); hộp thoại trợ giúp thêm liên kết «Bảo mật LAN (tài liệu)» khi tệp tồn tại (`PageHelpTexts`, `Ui_Help_Link_LanSecurityDoc`).

### Thêm / đổi (Vận hành và tin cậy — mục 2.1 tài liệu mở rộng)

- **Khôi phục kết nối:** checklist thêm bước 5 (Docker Engine / `docker info` trong distro); banner mất kết nối có dòng gợi ý + nút «Cài đặt (Kết nối)» (`OpenSettingsConnectionFromBannerCommand`) mở tab Kết nối (`SettingsViewModel.SelectedTabIndex`).

### Thêm / đổi (Compose — validate trước up, mục 2.3 tài liệu mở rộng)

- **Service Go:** `POST /api/compose/config/validate` — `docker compose config -q` trong thư mục project; OpenAPI và timeout dài giống các lệnh compose khác.
- **Client .NET / WPF:** `ComposeConfigValidateAsync`; tab Compose: nút «Kiểm tra config (-q)»; `compose up -d` chạy validate trước, thất bại thì không up (chuỗi `Ui_Compose_Status_ConfigValidateFailedBeforeUp`).

### Thêm / đổi (Kết nối nhất quán — mục 2.1 tài liệu mở rộng)

- **`WslServiceHealthCache.RefreshAsync`:** gọi song song GET `/api/health` và `/api/docker/info`, chỉ đặt cache «ổn» khi cả hai thành công (cùng tiêu chí với header và Tổng quan).
- **`AppShellActivityState.AttachHealthCache`:** gắn cache khi tạo shell; `ShouldRefresh*` (Container, Image, Log, Compose, Mạng/volume), `ShouldPollContainerStats` và `ShouldProcessLogsFollowFlush` trả về false khi `LastHealthy == false`; `ShouldAutoRefreshDashboard` không chặn để tab Tổng quan vẫn làm mới và phát hiện khôi phục.
- **Cài đặt:** `PostSaveHealthProbeAsync` và «Kiểm tra kết nối» cập nhật cache theo health + Docker; chuỗi toast sau Lưu (`Ui_Settings_Status_PostSaveConnectivityFail`).
- **Dọn dẹp:** chặn prune khi cache báo mất kết nối (`Ui_Cleanup_Status_ServiceDisconnected`).

### Thêm / đổi (Image — tìm và lọc; Container — gợi ý batch, mục 2.2 / 2.4 tài liệu mở rộng)

- **Image:** lọc Tất cả / Có tag / Dangling (khớp `repository` `<none>` từ API); ComboBox phạm vi tìm (`ImageSearchScope`); nhãn và tooltip từ `UiStrings`.
- **Container:** khối gợi ý thao tác hàng loạt (tuần tự API, hủy qua đóng xác nhận, stats batch 2–32 id, timeout HTTP); tooltip nút Stats batch chuyển sang tài nguyên chuỗi.

### Thêm / đổi (Container — tìm và lọc, mục 2.2 tài liệu mở rộng)

- **Danh sách container:** lọc theo enum (`ContainerListFilterKind`) thay vì chuỗi đã dịch; ComboBox **Phạm vi tìm** (`ContainerSearchScope`: mọi trường, tên/ID, image, trạng thái); nhãn và tooltip từ `UiStrings` (vi/en).

### Thêm / đổi (Cài đặt — vận hành và tin cậy, mục 2.1 tài liệu mở rộng)

- **`IAppSettingsStore`:** thuộc tính `SettingsFilePath` và `ExportToCopy` (sao chép `settings.json` hoặc ghi JSON từ `Load()` khi file chưa tồn tại).
- **Tab Kết nối:** hiển thị đường dẫn file cài đặt cục bộ và nút **Sao lưu file cài đặt ra…** (`SaveFileDialog`).
- **Chẩn đoán nhanh (API + WSL):** khối checklist bốn bước (địa chỉ, token, health + Docker, WSL) đầu kết quả; cập nhật `WslServiceHealthCache` theo **health + Docker** (không chỉ health); intro chuỗi vi/en cho tab Kết nối và WSL.

### Thêm / đổi (WSL — sẵn sàng thật, header đồng bộ Tổng quan, compose core, test, CI)

- **`WslDockerServiceAutoStart`:** probe ban đầu và vòng chờ sau spawn WSL / Start–Restart thủ công yêu cầu **cả** `/api/health` ổn định (hai GET liên tiếp, `Connection: close`, đọc hết body) **và** `/api/docker/info` (JSON envelope `success` + `data`); tách `TrySpawnWslRestartAndWaitForHealthAsync` (telemetry `startup_wsl_restart_recovery_spawned`) để tái sử dụng.
- **`AppStartupCoordinator`:** sau làm mới header, nếu bật tự khởi động WSL mà cache vẫn không «healthy» thì **một lần** gọi spawn restart + chờ connectivity; telemetry `app_startup_recovery_ensure_finished`.
- **`ShellCompositionResult` / `AppShellFactory`:** thêm `WslServiceHealthCache` vào composition để startup đọc `LastHealthy`.
- **`ShellViewModel.RefreshServiceHeaderFromApiAsync`:** gọi song song `GetHealthAsync` + `GetDockerInfoAsync`; nút Start/Stop/Restart và banner khớp trạng thái thật; khi health OK nhưng Docker không: chuỗi `Ui_Shell_ServiceHeader_HealthOkDockerDownFormat` và cache disconnected.
- **`DashboardViewModel`:** đầu `NotifyConnectivityChangeAsync` đồng bộ `WslServiceHealthCache` (OK / mất kết nối).
- **`DockLite.Core` — `ComposeComposePaths`:** parse nhiều dòng file compose, format editor, đối số `-f` + `BashSingleQuote` cho CLI; `ComposeViewModel` gọi helper (không đổi hành vi).
- **Test:** `DockLite.Tests` chuyển `net8.0-windows`, tham chiếu `DockLite.App` — `ComposeComposePathsTests`, `NetworkErrorMessageMapperTests`, thêm case clamp `HttpTimeoutSeconds` trong `AppSettingsDefaultsTests`.
- **CI:** job `dotnet` chạy trên `windows-latest` (test cần WPF/App); `dotnet test` không dùng `--no-restore` (restore tự động trên runner).
- **README:** mục «Kiểm thử trước release (definition of done)» (`dotnet test` + `bash scripts/test-go.sh` trong WSL); luồng mở app cập nhật mô tả probe health + Docker và bước khôi phục tùy chọn.

### Thêm / đổi (trung hạn: Compose nhiều -f, stats WebSocket, service Go)

- **Compose (UI + trợ giúp):** gợi ý rõ giới hạn tối đa 16 file `-f` (khớp validation Go); OpenAPI tag `compose` và mô tả API mô tả `X-Request-ID` / `req_id`.
- **Container (stats):** thêm lựa chọn interval WebSocket 3000 ms và 5000 ms; gợi ý dùng interval cao khi tải nặng; sparkline và mẫu stats lấy chuỗi từ `UiStrings` (vi/en).
- **Service Go:** middleware `LogRequests` chấp nhận header `X-Request-ID` (chuẩn hóa), luôn trả cùng giá trị trên response, gắn `req_id` vào `context` (`RequestIDFromContext`); `RequestContextTimeout` bổ sung deadline cho POST compose dài (30 phút), `POST /api/images/pull` và `/api/images/load` (30 phút), `POST /api/system/prune` (15 phút).
- **Client .NET:** `RequestIdDelegatingHandler` gửi `X-Request-ID` mỗi HTTP request; `CopyAuthorizationToWebSocket` thêm `X-Request-ID` cho mỗi kết nối WebSocket.

### Thêm / đổi (tối ưu ngắn hạn: tab, Cài đặt, test .NET)

- **Log follow:** khi rời tab Log (đang follow WebSocket), gọi `StopFollow()` để đóng luồng — giảm tải mạng; `AppShellActivityState` thêm `IsLogsPageVisible` / `IsMainWindowInteractive`.
- **Chẩn đoán nhanh WSL:** `QuickWslDiagnostics` thêm khối `uname`; sau khi chạy cập nhật `EffectiveWslPathSummary`; tab **Kết nối** có nút và ô kết quả (cùng `WslQuickDiagnosticsText` với tab WSL).
- **`ApiEnvelopeExtensions.ToApiResult`:** dùng trong `DockLiteApiClient.ReadEnvelopeAsync`; test `ApiResultEnvelopeTests`.

### Thêm / đổi (test tích hợp Docker, SBOM, release)

- `wsl-docker-service/integration/`: test build tag `integration` — `GET /api/health`, `GET /api/openapi.json`, `GET /api/docker/info` (bỏ qua nếu không có Docker Engine); `scripts/test-integration.sh`.
- CI: job `go-integration` (`docker info`, `go test -tags=integration ./integration/...`).
- Release: `.github/workflows/release.yml` khi đẩy tag `v*` — `docklite-wsl-linux-amd64`, `sbom-docklite-wsl.cdx.json` (Syft), `checksums-sha256.txt`, ký `cosign sign-blob` (`.sig`, `.cosign.bundle`).
- `docs/docklite-release-sbom.md`.

### Thêm / đổi (script test Go)

- `wsl-docker-service/scripts/test-go.sh`: `go vet ./...` và `go test ./...`; README ghi chú module không có `.go` ở gốc — không dùng `go test` không đối số; `build-server.sh` trỏ tới script và `./...`.

### Thêm / đổi (giới hạn tài nguyên WebSocket trên service Go)

- Gói `internal/wslimit`: semaphore kết nối đồng thời (mặc định 64, `DOCKLITE_WS_MAX_CONNECTIONS`, trần 4096), `Upgrader` buffer cố định, `SetReadLimit` tin nhắn từ client; handler log/stats trả 503 khi đủ slot.
- OpenAPI, `README`, `.env.example`, `internal/httpserver/limits.go` mô tả biến môi trường.

### Thêm / đổi (trợ giúp có liên kết, sao chép ID hàng loạt)

- Hộp thoại trợ giúp: `IDialogService.ShowHelpAsync` / `WpfDialogService` — nội dung văn bản và khối «Liên kết» (hyperlink mở trình duyệt); `PageHelpTexts.GetHelpLinksForPage` — OpenAPI theo base URL hiện tại, tài liệu ngoài theo từng màn (WSL, Docker, Compose, …); khóa `Ui_Help_*` (vi/en).
- Container / Image: nút «Sao chép ID đã chọn» — clipboard danh sách ID đầy đủ (một ID mỗi dòng); `ImagesViewModel` đồng bộ trạng thái nút khi tick ô chọn (`CanSelectAllFiltered`, `CanClearSelection`, `CanCopySelectedIds`, `CanBatchRemove`).

### Thêm / đổi (validation screen API, mapper HTTP, tách SettingsViewModel)

- `ScreenApiInputValidation` + kiểm tra trong `ContainerScreenApi` (ID container, clamp top 1–64, batch rỗng → danh sách rỗng), `ImageScreenApi`, `ComposeScreenApi` (project/service/exec/pull/remove).
- `NetworkErrorMessageMapper`: `HttpRequestException` theo mã 403, 404, 408, 429, 500, 502, 503; khóa `Ui_Error_Network_*` tương ứng (vi/en).
- `SettingsViewModel.WslCommands.cs` (partial): Start/Stop/Restart/Build WSL, đồng bộ mã, kiểm tra kết nối, `TruncateForToast`; gợi ý «chạy docklite-wsl» chỉ khi `HttpRequestException.StatusCode` null.

### Thêm / đổi (OpenAPI và CI)

- `GET /api/openapi.json`: phục vụ OpenAPI 3.0 (JSON) từ `wsl-docker-service/internal/httpserver/openapi.json`; README liệt kê endpoint.
- `.github/workflows/ci.yml`: `dotnet test` trên `DockLite.slnx`; trong `wsl-docker-service`: `go vet`, `go test`, `go build ./cmd/server`, job `go-integration` (Docker).
- `docs/docklite-system-analysis-and-improvements.md`: cập nhật checklist (auth, OpenAPI, CI, trợ giúp Cài đặt); mục 2.3–4–5–6–7 đồng bộ trạng thái hiện tại.

### Thêm / đổi (trợ giúp Cài đặt)

- `Ui_Help_Settings_Body` (vi/en) và `PageHelpTexts.SettingsBody`: gợi ý README, `docs/docklite-lan-security.md`, `docs/docklite-api-token.md`, endpoint `GET /api/openapi.json`.

### Thêm / đổi (xác thực API token)

- `AppSettings.ServiceApiToken` (JSON `ServiceApiToken`); tab **Kết nối**: ô token; `HttpClientAppSettings` gán `Authorization: Bearer`; WebSocket logs/stats sao chép header từ `HttpClient`.
- Service Go: biến `DOCKLITE_API_TOKEN` (khác rỗng) bật middleware `RequireBearerToken` (`Authorization: Bearer` hoặc `X-DockLite-Token`); rỗng = không đổi hành vi cũ.
- `NetworkErrorMessageMapper`: `HttpRequestException` với 401 → `Ui_Error_Network_Unauthorized`; gợi ý «chạy docklite-wsl» không thêm khi 401.

### Thêm / đổi (mapper lỗi mạng)

- `NetworkErrorMessageMapper`: giới hạn độ sâu duyệt ngoại lệ; `HttpIOException`; `WebException` (timeout / hủy / còn lại → gợi ý kết nối); `AuthenticationException` → `Ui_Error_Network_TlsOrCertificate`; `IOException` với thông điệp transport phổ biến; cuối cùng thử `InnerException` nếu bản đã ánh xạ khác `Message` gốc (bao bọc lỗi hiếm).

### Tài liệu

- `docs/docklite-api-token.md`: mẫu định dạng `DOCKLITE_API_TOKEN`, tạo token ngẫu nhiên (OpenSSL/PowerShell), tùy chọn SHA-256 từ passphrase; làm rõ server so khớp chuỗi (không lưu hash như mật khẩu).
- `docs/docklite-lan-security.md`: hướng dẫn bảo mật khi triển khai DockLite qua LAN (localhost so với expose, reverse proxy + HTTPS, firewall Windows, VPN, WebSocket qua proxy); liên kết từ README và cập nhật `docs/docklite-system-analysis-and-improvements.md` (checklist §5.3 / ưu tiên mục 3).
- `docs/docklite-api-auth-options.md`: so sánh header tĩnh với token trong cài đặt client + middleware Go; phương án B đã triển khai trong mã (token trong `settings.json` + `DOCKLITE_API_TOKEN`).

### Thêm / đổi (ổn định vận hành: thông báo kết nối và telemetry cục bộ opt-in)

- `AppSettings.DiagnosticLocalTelemetryEnabled` (mặc định tắt); tab **Chờ và health**: checkbox ghi file `docklite-diagnostic-*.log` cùng thư mục log ứng dụng — sự kiện tối giản (kết quả `TryEnsureRunningAsync`, spawn WSL restart, timeout health, `http_read_exhausted` sau retry HTTP); không gửi mạng, không ghi mật khẩu hay stack đầy đủ.
- `DiagnosticTelemetry` + `DiagnosticTelemetry.SetEnabled` khi load shell và sau **Lưu**.
- `HttpReadRetry`: sau hết lần thử ném lại `HttpRequestException` / `TaskCanceledException` (không còn `InvalidOperationException` rỗng); ghi telemetry khi bật opt-in.
- `NetworkErrorMessageMapper` (thay `DockLite.Core.ExceptionMessages` đã gỡ): ánh xạ `HttpRequestException` / `SocketException` / timeout / `HttpReadRetry` / hủy sang chuỗi đã dịch (`Ui_Error_Network_*`, `Ui_Status_Common_CancelledShort` trong `UiStrings.vi/en.xaml`); `App.xaml.cs` và ViewModel dùng `FormatForUser` từ lớp này.
- Telemetry (opt-in) bổ sung: `WriteManualWslLifecycle` — nút Start/Stop/Restart service WSL trên header và Cài đặt, Build; `WriteTestConnection` — «Kiểm tra kết nối»; `WriteSyncCodeToWsl` — «Đồng bộ mã → WSL».
- Sidebar: chấm tròn cạnh tiêu đề — màu theo kết nối `/api/health` (xanh / đỏ / xám), tooltip vi/en (`Ui_Sidebar_ConnectionTooltip`).
- Màn hình (WPF): lớp service bọc `IDockLiteApiClient` theo tab — `IContainerScreenApi` / `ContainerScreenApi`, `IImageScreenApi` / `ImageScreenApi`, `ISystemDiagnosticsScreenApi` / `SystemDiagnosticsScreenApi` (Tổng quan, Cài đặt, Shell; `WslServiceHealthCache.RefreshAsync` nhận interface này), `ILogsScreenApi` / `LogsScreenApi` (danh sách container + tail log), `IComposeScreenApi` / `ComposeScreenApi`, `INetworkVolumeScreenApi` / `NetworkVolumeScreenApi`, `ICleanupScreenApi` / `CleanupScreenApi`; ViewModel tương ứng inject interface; `AppShellFactory` tạo một `DockLiteApiClient` và các screen API ủy quyền tới client đó.

### Thêm / đổi (i18n trang Tổng quan)

- `UiStrings.vi/en.xaml`: khóa `Ui_Dashboard_*` (dòng trạng thái health, định dạng thông tin Docker, toast kết nối lại / mất kết nối, thông báo lỗi kết nối); `DashboardViewModel` đọc qua `UiLanguageManager`.

### Thêm / đổi (i18n header Shell và hộp thoại lỗi toàn cục)

- `UiStrings.vi/en.xaml`: khóa `Ui_Shell_*` (dòng trạng thái service trên header, tiến trình khởi động WSL / chờ health, toast chờ health, định dạng dòng health có phiên bản); `ShellViewModel` và tiêu đề `MessageBox` lỗi UI trong `App.xaml.cs` dùng `Ui_MainWindow_Title` / `UiLanguageManager`.

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
- Tuỳ chọn: API batch stats một lần nhiều container (TLS tùy chọn và pin TOFU đã gộp vào phát hành 2.0.0).

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
