# Architecture

**Analysis Date:** 2026-04-23

## Pattern Overview

**Overall:** Ứng dụng desktop Windows (WPF + MVVM) đóng vai client của một sidecar HTTP/WebSocket (Go) chạy trong WSL; sidecar bao quanh Docker Engine API. Phần .NET áp dụng Clean Architecture rút gọn (App → Infrastructure → Core → Contracts). Phần Go áp dụng layout chuẩn `cmd/` + `internal/` với các package phẳng theo domain.

**Key Characteristics:**
- Hai process tách biệt: WPF (Windows) và Go daemon (`docklite-wsl`) trong WSL; giao tiếp qua HTTP REST + WebSocket trên `127.0.0.1:17890`.
- WPF là thin client MVVM: mọi thao tác Docker đều đi qua `IDockLiteApiClient` / `ILogStreamClient` / `IStatsStreamClient` — không gọi trực tiếp Docker Engine từ Windows.
- Go daemon là stateless HTTP server; Docker được truy cập qua singleton `github.com/docker/docker/client` (Docker Engine API), không shell ra `docker` CLI cho hầu hết thao tác.
- Các hợp đồng API (envelope, DTO) đặt trong project .NET `DockLite.Contracts` và được phản ánh song song trong Go (`internal/apiresponse`, cấu trúc handler). Không có code-gen chung; đồng bộ thủ công qua `openapi.json`.
- Vòng đời sidecar do App điều phối (auto-start qua `wsl.exe bash scripts/restart-server.sh` khi health probe thất bại).

## Layers

**UI Layer (WPF/XAML) — `DockLite.App`:**
- Purpose: Cửa sổ, XAML, DataTemplate, theme, i18n resource, ghi đè hành vi chuột/keyboard.
- Contains: `App.xaml.cs`, `MainWindow.xaml[.cs]`, `Views/*.xaml[.cs]`, `Themes/*.xaml`, `Resources/UiStrings.*.xaml`, `Converters/`, `Behaviors/`.
- Location: `src/DockLite.App/Views/`, `src/DockLite.App/Themes/`, `src/DockLite.App/Resources/`, `src/DockLite.App/Converters/`, `src/DockLite.App/Behaviors/`.
- Depends on: ViewModel layer, `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection`.
- Used by: Người dùng cuối (tương tác UI).

**ViewModel Layer — `DockLite.App/ViewModels`:**
- Purpose: Binding state cho từng tab, điều phối lệnh, gộp/debounce refresh, gắn progress.
- Contains: `ShellViewModel`, `DashboardViewModel`, `ContainersViewModel` (+ partial `BatchStats`), `ImagesViewModel`, `LogsViewModel`, `ComposeViewModel`, `SettingsViewModel` (+ partial `WslCommands`), `NetworkVolumeViewModel`, `CleanupViewModel`, `DockerEventsViewModel`, `AppDebugLogViewModel`.
- Location: `src/DockLite.App/ViewModels/`.
- Depends on: `Services/*ScreenApi`, `AppShellActivityState`, `WslServiceHealthCache`, `IDialogService`, `INotificationService`, `IAppShutdownToken`, `Core.Services.ILogStreamClient` / `IStatsStreamClient`, `Contracts.Api.*`.
- Used by: Views (qua `DataContext`); `ShellViewModel` được tạo bởi `AppShellFactory`.

**App Services Layer (UI-side adapters) — `DockLite.App/Services`:**
- Purpose: Dịch `IDockLiteApiClient` thô thành API «theo màn hình» (validate input, map lỗi), cung cấp dialog/toast/theme/i18n/cờ hoạt động shell.
- Contains: `*ScreenApi` (`ContainerScreenApi`, `ImageScreenApi`, `ComposeScreenApi`, `LogsScreenApi`, `NetworkVolumeScreenApi`, `CleanupScreenApi`, `SystemDiagnosticsScreenApi`), `WpfDialogService`, `WpfToastNotificationService`, `ThemeManager`, `UiLanguageManager`, `AppShellActivityState`, `WslServiceHealthCache`, `ScreenApiInputValidation`, `NetworkErrorMessageMapper`, `AppShutdownToken`, `MainWindowAccessor`.
- Location: `src/DockLite.App/Services/`.
- Depends on: `Core.Services.IDockLiteApiClient`, `Contracts.Api.*`, WPF primitives (`System.Windows`).
- Used by: ViewModels, `AppShellFactory`, `AppStartupCoordinator`.

**Core Layer (domain contracts + logic thuần .NET) — `DockLite.Core`:**
- Purpose: Định nghĩa interface API client, cấu hình ứng dụng, đường dẫn/định danh domain (compose paths, source version, URL helper) và telemetry/log nội bộ.
- Contains: `Services/IDockLiteApiClient.cs`, `Services/ILogStreamClient.cs`, `Services/IStatsStreamClient.cs`, `Configuration/AppSettings.cs` (+ `AppSettingsDefaults`, `IAppSettingsStore`, `HttpClientAppSettings`, `ServiceBaseUriHelper`, `DockLiteDefaults`), `Compose/ComposeComposePaths.cs`, `Diagnostics/AppFileLog.cs`, `Diagnostics/DiagnosticTelemetry.cs`, `DockLiteSourceVersion.cs`.
- Location: `src/DockLite.Core/`.
- Depends on: `DockLite.Contracts` (DTO/envelope); không phụ thuộc WPF, HTTP hay WSL.
- Used by: `DockLite.Infrastructure`, `DockLite.App`.

**Contracts Layer — `DockLite.Contracts`:**
- Purpose: Hợp đồng wire-format dùng chung giữa WPF và Go daemon — envelope JSON `{code, message, requestId, data}`, DTO cho container/image/volume/network/compose, mã lỗi chuẩn.
- Contains: `Api/ApiEnvelope.cs`, `Api/ApiResult.cs`, `Api/ApiErrorBody.cs`, `Api/ApiEnvelopeExtensions.cs`, `Api/DockLiteErrorCodes.cs`, `Api/HealthResponse.cs`, các `*Data` / `*Dto` / `*Request`.
- Location: `src/DockLite.Contracts/Api/`.
- Depends on: Không — project lá.
- Used by: Core, Infrastructure, App. Bên Go có cấu trúc tương đương trong `internal/apiresponse` và từng handler.

**Infrastructure Layer — `DockLite.Infrastructure`:**
- Purpose: Cài đặt cụ thể `IDockLiteApiClient`, client streaming (logs, stats), client HTTP chung với reconfigure runtime, lưu cài đặt xuống đĩa, điều phối vòng đời WSL sidecar và chuẩn hóa đường dẫn WSL.
- Contains:
  - `Api/DockLiteApiClient.cs`, `Api/DockLiteHttpSession.cs`, `Api/RequestIdDelegatingHandler.cs`, `Api/HttpReadRetry.cs`, `Api/LogStreamClient.cs`, `Api/StatsStreamClient.cs`.
  - `Configuration/AppSettingsStore.cs`, `Configuration/DockLiteApiDefaults.cs`.
  - `Wsl/WslDockerServiceAutoStart.cs`, `Wsl/WslDistroProbe.cs`, `Wsl/WslHostAddressResolver.cs`, `Wsl/WslPathNormalizer.cs`, `Wsl/WslPathProbe.cs`, `Wsl/WslDockerServicePathResolver.cs`, `Wsl/WslStartupProgress.cs`, `Wsl/WslEnsureFailureReason.cs`.
- Location: `src/DockLite.Infrastructure/`.
- Depends on: `DockLite.Core`, `DockLite.Contracts`, `System.Net.Http`, `System.Diagnostics.Process` (spawn `wsl.exe`).
- Used by: `AppShellFactory`, `AppStartupCoordinator`, `*ScreenApi`, ViewModels.

**Composition Root — `DockLite.App` (root)**
- Purpose: DI container, factory tạo shell, khởi động ban đầu và bắt exception toàn cục.
- Contains: `App.xaml.cs`, `AppHostContext.cs`, `ServiceCollectionExtensions.cs`, `AppShellFactory.cs` + `IAppShellFactory.cs`, `AppStartupCoordinator.cs`, `MainWindow.xaml.cs`.
- Depends on: Mọi layer .NET ở trên.

**Go Sidecar Layers — `wsl-docker-service/`:**

- `cmd/server/main.go` — Entry point daemon Go: lắng nghe `0.0.0.0:17890` (override qua `DOCKLITE_ADDR`), build middleware chain `LogRequests → RequestContextTimeout → LimitRequestBody → [RequireBearerToken] → mux`.
- `internal/httpserver/` — Routing (`register.go`), middleware (`logging.go`, `auth.go`, `limits.go`, `context_timeout.go`), metrics (`metrics.go`), phát OpenAPI (`openapi.go` + `openapi.json`), request-id helpers.
- `internal/docker/` — Handler REST cho containers/images/networks/volumes/system prune, streaming events, stats batch, trivy scan; dùng Docker Engine API.
- `internal/dockerengine/` — Singleton Docker Engine client (`client.go`) và map lỗi (`apierr.go`).
- `internal/dockercli/` — Shim cho các thao tác còn phải gọi `docker` CLI (ví dụ một vài thao tác không có trong SDK).
- `internal/compose/` — Handler compose: gọi `docker compose` CLI có tham số an toàn, cache project, profiles, services.
- `internal/ws/` — Upgrade WebSocket cho logs container và stream nội bộ; dùng `github.com/gorilla/websocket`.
- `internal/hostresources/` — Endpoint `/api/wsl/host-resources` đọc RAM/CPU/disk ở host Linux trong WSL.
- `internal/wslimit/` — Parse giới hạn WSL từ `/proc` hoặc cgroup.
- `internal/apiresponse/` — Bộ helper envelope (code, message, requestId, data) — gương của `DockLite.Contracts.Api.ApiEnvelope`.
- `internal/appversion/` — Đọc `VERSION` nhúng, trả cho `/api/health`.
- `internal/settings/` — Placeholder cho cấu hình server (`doc.go`).
- `integration/` — Test tích hợp API (`api_integration_test.go`) chạy qua `scripts/test-integration.sh`.

## Data Flow

**Lệnh người dùng (ví dụ «Stop container»):**

1. Người dùng nhấn nút trong `src/DockLite.App/Views/ContainersView.xaml` — lệnh bound tới `ContainersViewModel`.
2. `ContainersViewModel` (trong `src/DockLite.App/ViewModels/ContainersViewModel.cs`) kiểm tra `AppShellActivityState` và `WslServiceHealthCache`, sau đó gọi `IContainerScreenApi.StopContainerAsync(...)` ở `src/DockLite.App/Services/ContainerScreenApi.cs`.
3. `ContainerScreenApi` validate input bằng `ScreenApiInputValidation`, rồi gọi `IDockLiteApiClient.StopContainerAsync(...)` ở `src/DockLite.Infrastructure/Api/DockLiteApiClient.cs`.
4. `DockLiteApiClient` dùng `DockLiteHttpSession.Client` (HTTP client có `RequestIdDelegatingHandler`) gửi `POST http://127.0.0.1:17890/api/containers/{id}/stop`.
5. Trên Go: `cmd/server/main.go` nhận request; middleware `LogRequests → RequestContextTimeout → LimitRequestBody → (auth tuỳ chọn)` chạy trước `mux.HandleFunc("/api/containers/", docker.ContainersItem)` trong `wsl-docker-service/internal/httpserver/register.go`.
6. Handler `docker.ContainersItem` trong `wsl-docker-service/internal/docker/containers.go` parse path/method, lấy singleton `dockerengine.Client()` và gọi Docker Engine API qua UNIX socket mặc định (`/var/run/docker.sock` qua `client.FromEnv`).
7. Kết quả map qua `internal/apiresponse` thành envelope JSON chuẩn và trả về.
8. Phía .NET deserialize thành `ApiResult<EmptyApiPayload>`; ViewModel cập nhật UI (toast qua `INotificationService`, refresh list, set busy flag).

**Streaming (logs / stats):**

- Logs: `LogsViewModel` đăng ký với `ILogStreamClient` (`src/DockLite.Infrastructure/Api/LogStreamClient.cs`), client mở WebSocket `ws://127.0.0.1:17890/ws/containers/{id}/logs` tới `internal/ws/logs.go`; dữ liệu được gộp/debounce bởi `SearchDebounceHelper` và model phân loại `LogLineClassifier` trước khi hiển thị.
- Stats: `StatsStreamClient` + `internal/docker/stats_ws.go`; kết hợp HTTP `POST /api/containers/stats-batch` cho snapshot, ViewModel điều khiển polling bằng `AppShellActivityState.ShouldPollContainerStats`.
- Docker events: `DockerEventsViewModel` → `IDockLiteApiClient` stream tới `internal/docker/events_stream.go` (`GET /api/docker/events/stream`, timeout context = 0 để không cắt SSE).

**Khởi động ứng dụng:**

1. `App.xaml.cs::OnStartup` gắn 3 handler exception toàn cục, tạo `ServiceCollection`, gọi `services.AddDockLiteUi(AppContext.BaseDirectory)` (`src/DockLite.App/ServiceCollectionExtensions.cs`).
2. DI resolve `ShellCompositionResult` — factory `AppShellFactory.Create(...)` (`src/DockLite.App/AppShellFactory.cs`) đọc `AppSettings` từ đĩa qua `AppSettingsStore`, bật telemetry, apply theme/i18n, tạo `DockLiteHttpSession`, khởi tạo `DockLiteApiClient` + các `*ScreenApi`, tạo `ShellViewModel` với `Lazy<>` cho từng tab để tránh dựng ViewModel không dùng.
3. DI resolve `MainWindow` — constructor gắn `DataContext = ShellViewModel`, hook `Loaded/Activated/Deactivated/StateChanged` để cập nhật `AppShellActivityState`.
4. Khi `MainWindow.Loaded`, `IAppStartupService` (`AppStartupCoordinator`) gọi `WslDockerServiceAutoStart.TryEnsureRunningAsync` — nếu GET `/api/health` fail và `AutoStartWslService` bật: spawn `wsl.exe bash -lc "cd '<wsl-path>' && exec bash scripts/restart-server.sh"` (hoặc `run-server.sh`), poll health đến khi healthy hoặc timeout; progress được đẩy vào `ShellViewModel` qua `IProgress<WslStartupProgress>`.
5. Sau khi health OK, dashboard refresh và `WslServiceHealthCache` phát `Changed` để đồng bộ header + quyết định polling.

**State Management:**
- Phía .NET: stateful trong RAM (ViewModel, cache health, HTTP client). Cấu hình người dùng lưu qua `AppSettingsStore` vào đĩa (đọc 1 lần lúc khởi động, `DockLiteHttpSession.Reconfigure` khi người dùng Lưu cài đặt).
- Phía Go: gần như stateless; chỉ có singleton Docker Engine client và cache compose project trong process (`internal/compose/compose_cache.go`).
- Không có database; không persistent state ở sidecar.

## Key Abstractions

**API Client Contract — `IDockLiteApiClient`:**
- Purpose: Bề mặt duy nhất để WPF gọi REST sidecar; mọi endpoint Docker trả `ApiResult<TData>` (envelope hợp nhất).
- Examples: `src/DockLite.Core/Services/IDockLiteApiClient.cs`, cài đặt tại `src/DockLite.Infrastructure/Api/DockLiteApiClient.cs`.
- Pattern: Interface ở Core, implementation ở Infrastructure (Dependency Inversion).

**API Envelope — `ApiEnvelope<T>` / `ApiResult<T>` / `ApiErrorBody`:**
- Purpose: Chuẩn hóa wire format `{code, message, requestId, data}`; tách rõ success/error mà không dùng exception xuyên network.
- Examples: `src/DockLite.Contracts/Api/ApiEnvelope.cs`, `ApiResult.cs`, `ApiEnvelopeExtensions.cs`, `DockLiteErrorCodes.cs`.
- Pattern: Result type (mirror trong Go ở `wsl-docker-service/internal/apiresponse/apiresponse.go`).

**Screen API — `I*ScreenApi`:**
- Purpose: Adapter theo màn hình, validate input và dịch lỗi mạng sang message người dùng, để ViewModel mỏng.
- Examples: `src/DockLite.App/Services/IContainerScreenApi.cs` + `ContainerScreenApi.cs`, `ImageScreenApi.cs`, `ComposeScreenApi.cs`, `LogsScreenApi.cs`, `NetworkVolumeScreenApi.cs`, `CleanupScreenApi.cs`, `SystemDiagnosticsScreenApi.cs`.
- Pattern: Facade per-view over `IDockLiteApiClient`.

**Streaming Clients — `ILogStreamClient`, `IStatsStreamClient`:**
- Purpose: Tách WebSocket logs/stats khỏi REST client để có lifecycle riêng (cancel/backoff).
- Examples: `src/DockLite.Core/Services/ILogStreamClient.cs`, `IStatsStreamClient.cs` + `src/DockLite.Infrastructure/Api/LogStreamClient.cs`, `StatsStreamClient.cs`.
- Pattern: Observer / async enumerator.

**Activity / Health Cache — `AppShellActivityState`, `WslServiceHealthCache`:**
- Purpose: Singleton pub/sub để quyết định khi nào tạm dừng polling (cửa sổ minimize, tab không active, hoặc mất kết nối).
- Examples: `src/DockLite.App/Services/AppShellActivityState.cs`, `WslServiceHealthCache.cs`.
- Pattern: Mediator-lite + event `Changed`.

**WSL Lifecycle Helper — `WslDockerServiceAutoStart`:**
- Purpose: Gom mọi heuristic spawn `wsl.exe`, chờ health, đo thời gian, thu output stderr/stdout gần nhất để chẩn đoán.
- Examples: `src/DockLite.Infrastructure/Wsl/WslDockerServiceAutoStart.cs`, `WslStartupProgress.cs`, `WslEnsureFailureReason.cs`.
- Pattern: Static coordinator giữ `Process` list để giữ event reader khỏi GC.

**Shell Composition — `ShellCompositionResult` + `AppShellFactory`:**
- Purpose: Bundle root ViewModel + `HttpSession` + snapshot `AppSettings` + `WslServiceHealthCache` thành một record bất biến cho DI.
- Examples: `src/DockLite.App/AppShellFactory.cs` (`ShellCompositionResult` record, `AppShellFactory` implementation).
- Pattern: Factory + immutable record.

**HTTP Session — `DockLiteHttpSession`:**
- Purpose: Gói `HttpClient` có thể thay mới khi user đổi cài đặt; client cũ được dispose trễ để tránh `ObjectDisposedException`.
- Examples: `src/DockLite.Infrastructure/Api/DockLiteHttpSession.cs`, delegating handler `RequestIdDelegatingHandler`.
- Pattern: Hot-swappable singleton.

**Go Engine Singleton — `dockerengine.Client()`:**
- Purpose: Một `*client.Client` dùng lại cho mọi handler; khởi tạo bằng `client.FromEnv + WithAPIVersionNegotiation`.
- Examples: `wsl-docker-service/internal/dockerengine/client.go`.
- Pattern: `sync.Once` lazy singleton.

**Go Middleware Chain:**
- Purpose: Composable `http.Handler` bọc mux: `LogRequests → RequestContextTimeout → LimitRequestBody → (RequireBearerToken) → mux`.
- Examples: `wsl-docker-service/cmd/server/main.go`, `wsl-docker-service/internal/httpserver/logging.go`, `auth.go`, `limits.go`, `context_timeout.go`.
- Pattern: Decorator / middleware chain.

## Entry Points

**WPF App Entry — `App.OnStartup`:**
- Location: `src/DockLite.App/App.xaml` (startup URI implicit) + `src/DockLite.App/App.xaml.cs` (`OnStartup`).
- Triggers: Người dùng chạy `DockLite.exe` (Windows).
- Responsibilities: Gắn handler exception toàn cục (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) ghi qua `AppFileLog`; build DI qua `ServiceCollectionExtensions.AddDockLiteUi`; resolve `ShellCompositionResult` + `MainWindow` và hiển thị.

**Main Window Lifecycle — `MainWindow`:**
- Location: `src/DockLite.App/MainWindow.xaml` + `src/DockLite.App/MainWindow.xaml.cs`.
- Triggers: DI resolve sau `OnStartup`.
- Responsibilities: Đặt `DataContext = ShellViewModel`, chạy `IAppStartupService.RunInitialLoadAsync` khi `Loaded`, cập nhật `AppShellActivityState` khi Activated/Deactivated/StateChanged, hủy `AppShutdownToken` khi `Closed`.

**App Startup Coordinator — `AppStartupCoordinator.RunInitialLoadAsync`:**
- Location: `src/DockLite.App/AppStartupCoordinator.cs`.
- Triggers: `MainWindow.Loaded`.
- Responsibilities: Ensure sidecar WSL chạy (`WslDockerServiceAutoStart.TryEnsureRunningAsync`), xử lý recovery một lần nếu health cache vẫn `false`, đẩy progress vào shell, refresh dashboard.

**Go Daemon Entry — `main.main`:**
- Location: `wsl-docker-service/cmd/server/main.go`.
- Triggers: Script `wsl-docker-service/scripts/run-server.sh` (hoặc `restart-server.sh`) thực thi binary tại `wsl-docker-service/bin/docklite-wsl`; script này lại được `wsl.exe bash -lc` gọi bởi `WslDockerServiceAutoStart` hoặc `scripts/Start-DockLiteWsl.ps1`.
- Responsibilities: Đọc `DOCKLITE_ADDR` / `DOCKLITE_API_TOKEN`, dựng mux + middleware chain, `http.Server` với timeouts từ `httpserver` constants, `ListenAndServe`.

**PowerShell Bootstrap — `scripts/Start-DockLiteWsl.ps1`:**
- Location: `scripts/Start-DockLiteWsl.ps1` (Windows host).
- Triggers: Dev chạy thủ công khi không muốn App auto-start sidecar.
- Responsibilities: Resolve đường dẫn Windows → WSL qua `wsl wslpath -a`, gọi `wsl.exe [-d <distro>] bash -lc "cd '<wsl-path>' && exec bash scripts/run-server.sh"`.

## Threading & Process Model

- WPF UI thread chạy Dispatcher; mọi await trong ViewModel đều `.ConfigureAwait(true)` khi cần quay lại UI (xem `AppStartupCoordinator.cs`) hoặc `.ConfigureAwait(false)` khi không.
- Timer polling stats/dashboard là async với điều kiện cắt qua `AppShellActivityState.ShouldPoll*` — tránh chạy khi minimize / tab khác.
- `AppShutdownToken` (wrap `CancellationTokenSource`) được hủy khi `MainWindow.Closed`; mọi request dài hạn đều link vào token này qua `CancellationTokenSource.CreateLinkedTokenSource`.
- `DockLiteHttpSession.Reconfigure` đổi `HttpClient` nóng; client cũ được dispose sau `oldClient.Timeout + 2s` (tối đa 12 phút) để request đang chạy kết thúc sạch.
- Phía Go: mỗi request là một goroutine do `net/http` tạo. Context có deadline theo `timeoutForRequest` (`internal/httpserver/context_timeout.go`) — stream/WS có `timeout = 0`.
- Sidecar process được spawn bằng `System.Diagnostics.Process` với `wsl.exe bash -lc ...`; tham chiếu giữ trong `WslDockerServiceAutoStart.WslRunServerProcesses` để không mất stdout/stderr reader.

## Go Sidecar Lifecycle

1. App probe `GET http://<base-url>/api/health` ngắn (`HealthProbeSingleRequestSeconds`, mặc định 3s).
2. Nếu thất bại và `AppSettings.AutoStartWslService = true`: `WslDockerServiceAutoStart` chạy `wsl.exe [-d <distro>] bash -lc "cd '<wsl-unix-path>' && bash scripts/restart-server.sh"`. Script dừng tiến trình `bin/docklite-wsl` cũ (`pkill -f bin/docklite-wsl`) rồi `exec bash scripts/run-server.sh` để start binary đã build sẵn trong `wsl-docker-service/bin/docklite-wsl`.
3. App poll lại health ở interval `WslHealthPollIntervalMilliseconds` (mặc định 500ms) tới tối đa `WslAutoStartHealthWaitSeconds` giây (mặc định 30s, 90s cho lần restart thủ công).
4. Nếu health timeout, `AppStartupCoordinator` hiển thị toast `DockLite — health timeout` và gợi ý đọc log `AppFileLog.LogDirectory`.
5. Khi App đóng: `AppShutdownToken.Cancel` hủy mọi request; process Go vẫn chạy trong WSL (không kill khi App thoát — theo thiết kế hiện tại).

## Error Handling

**Strategy:** Không ném exception xuyên biên giới process. Go trả envelope có `code`/`message`/`requestId`, WPF map thành `ApiResult<T>.IsSuccess = false` và hiển thị qua `NetworkErrorMessageMapper.FormatForUser`.

**Patterns:**
- Exception toàn cục WPF: `App.OnDispatcherUnhandledException` set `e.Handled = true`, log qua `AppFileLog.WriteException`, hiển thị `MessageBox` (tiêu đề i18n qua `UiLanguageManager`).
- Mỗi `*ScreenApi` catch `HttpRequestException` / `OperationCanceledException` và chuyển thành `ApiResult` lỗi có message thân thiện (xem `NetworkErrorMessageMapper` ở `src/DockLite.App/Services/NetworkErrorMessageMapper.cs`).
- `HttpReadRetry` (`src/DockLite.Infrastructure/Api/HttpReadRetry.cs`) retry đọc body khi Windows reset connection ngắn sau resume WSL.
- Phía Go: mỗi handler trả `apiresponse.WriteError(code, message, status)` (`wsl-docker-service/internal/apiresponse/apiresponse.go`), error từ Docker Engine được map qua `internal/dockerengine/apierr.go`.
- `OperationCanceledException` khi đóng App được nuốt im trong `AppStartupCoordinator` để tránh spam lỗi.

## Cross-Cutting Concerns

**Logging:**
- WPF: `AppFileLog.Write(tag, message)` ghi file trong `AppFileLog.LogDirectory`; tab «App debug log» (`AppDebugLogViewModel`) đọc lại file đó.
- Telemetry diagnostic opt-in qua `DiagnosticTelemetry` (bật bởi `AppSettings.DiagnosticLocalTelemetryEnabled`); ghi event cấu trúc (`app_startup_ensure_finished`, ...).
- Go: `slog` mặc định; middleware `httpserver.LogRequests` log mỗi request một dòng (`internal/httpserver/logging.go`).

**Validation:**
- Input ViewModel → `*ScreenApi` qua `ScreenApiInputValidation` (trim, check rỗng, ràng buộc container id / image tag).
- Validate compose command qua `ComposeServiceRequest`, `ComposeIdRequest`.
- Phía Go: method check, parse path thủ công, `LimitRequestBody` (8 MiB mặc định).

**Authentication:**
- Mặc định không auth (sidecar chỉ nghe `127.0.0.1:17890`).
- Tuỳ chọn bật `DOCKLITE_API_TOKEN` (env var phía Go) → middleware `httpserver.RequireBearerToken` yêu cầu `Authorization: Bearer <token>`. Token đồng thời được WPF đặt qua `HttpClientAppSettings.ApplyTo` (xem `src/DockLite.Core/Configuration/HttpClientAppSettings.cs`).
- Request-id round-trip: `RequestIdDelegatingHandler` thêm header, Go echo lại trong envelope để trace.

**Localization & Theming:**
- i18n qua `UiLanguageManager` + `Resources/UiStrings.en.xaml` / `UiStrings.vi.xaml`.
- Theme qua `ThemeManager` + `Themes/ModernTheme.xaml` / `DarkTheme.xaml`, có listener theo theme hệ thống (`WindowsSystemTheme`).

**Configuration:**
- Nguồn duy nhất: `AppSettings` (`src/DockLite.Core/Configuration/AppSettings.cs`) + defaults (`AppSettingsDefaults`), đọc/ghi qua `IAppSettingsStore` → `AppSettingsStore` (persist file trong LocalAppData).
- Base URL service tính qua `ServiceBaseUriHelper` và `DockLiteApiDefaults`.

---

*Architecture analysis: 2026-04-23*
