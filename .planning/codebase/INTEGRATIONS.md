# External Integrations

**Analysis Date:** 2026-04-23

## APIs & External Services

**Docker Engine (local socket trong WSL):**
- Tích hợp chính của toàn hệ thống — mọi thao tác container, image, network, volume, events, compose.
- Tích hợp trên Unix socket mặc định (hoặc `DOCKER_HOST` nếu đặt); không dùng TCP remote.
- SDK/Client: `github.com/docker/docker` v26.1.5+incompatible với `client.NewClientWithOpts(client.FromEnv, client.WithAPIVersionNegotiation())` — singleton tại `wsl-docker-service/internal/dockerengine/client.go`.
- Auth: không dùng token; truy cập qua quyền Unix socket của user chạy service trong WSL (thường là user trong nhóm `docker`).
- API nội bộ dùng:
  - Engine API trực tiếp: list/inspect/start/stop/restart/remove container (`wsl-docker-service/internal/docker/containers.go`, `container_detail.go`), stats (`container_stats_batch.go`, `stats_ws.go`, `container_stats_top.go`), logs (`wsl-docker-service/internal/ws/logs.go`), image list/inspect/history/pull/load/remove/prune (`wsl-docker-service/internal/docker/images.go`, `image_detail.go`), network/volume (`networks.go`, `volumes.go`), system prune (`system_prune.go`), events stream NDJSON (`events_stream.go`), info/health (`health.go`).
- Lệnh spawn CLI `docker` (thay vì Engine API): chỉ cho Docker Compose — `wsl-docker-service/internal/compose/compose.go:488` (`exec.CommandContext(ctx, "docker", fullArgs...)` cho `docker compose up|down|ps|...`) và `wsl-docker-service/internal/compose/compose_services.go:102,143,286` (list services, exec, logs). `docker` và `docker compose` plugin phải có trong `PATH` của WSL.

**Trivy (tùy chọn, công cụ ngoài):**
- Dịch vụ: [Trivy CLI](https://github.com/aquasecurity/trivy) quét CVE cho image Docker.
- Tích hợp: `wsl-docker-service/internal/docker/image_trivy_scan.go` — handler `POST /api/images/trivy-scan` kiểm tra `exec.LookPath("trivy")`; nếu không có, trả `503 Service Unavailable` với lỗi hiển thị cho người dùng. Đầu vào `imageRef`, `format` (`table`|`json`), `policyPath` (tuyệt đối trong WSL, không được chứa `..` hay `;|&\`$`).
- Auth: không (Trivy tự quản lý cache/DB offline).

**External APIs (Internet):**
- Không. Service Go và ứng dụng WPF không gọi bất kỳ API bên thứ ba nào. Mọi lưu lượng mạng giới hạn trong `localhost` / WSL loopback, trừ khi Docker Engine/Trivy tự pull image/DB từ registry của riêng chúng.

## Data Storage

**Databases:**
- Không có database server. Không có ORM hay client DB trong `src/` hoặc `wsl-docker-service/`.

**File Storage (cục bộ):**
- Cấu hình ứng dụng Windows: `%LocalAppData%\DockLite\settings.json` (đọc/ghi bởi `src/DockLite.Infrastructure/Configuration/AppSettingsStore.cs` qua `System.Text.Json`).
- Danh sách compose project phía WSL: `~/.docklite/compose_projects.json` trong distro (quản lý bởi `wsl-docker-service/internal/compose/compose.go`).
- Log ứng dụng Windows: `%LocalAppData%\DockLite\logs\docklite-*.log` và tùy chọn `docklite-diagnostic-*.log` (bật qua cờ `DiagnosticLocalTelemetryEnabled` trong `AppSettings`; ghi file cục bộ, không gửi ra mạng — xem `src/DockLite.Core/Diagnostics/DiagnosticTelemetry.cs`, `AppFileLog.cs`).
- Cache version service Go: `wsl-docker-service/internal/appversion/VERSION` (nhúng vào binary bằng `//go:embed`).
- File môi trường mẫu: `wsl-docker-service/.env.example` (không commit `.env` — đã khai báo trong `.gitignore`).

**Caching:**
- Không có cache server (Redis, Memcached). Trong process:
  - `wsl-docker-service/internal/compose/compose_cache.go` - cache dữ liệu compose trong bộ nhớ tiến trình service.
  - `src/DockLite.App/Services/WslServiceHealthCache.cs` - cache trạng thái health của service Go phía WPF.

## Authentication & Identity

**Auth giữa WPF và service Go:**
- Tùy chọn, tắt mặc định. Bật bằng cách đặt biến môi trường `DOCKLITE_API_TOKEN` khác rỗng phía WSL và nhập cùng giá trị vào trường **Token API** trong màn Cài đặt trên Windows (lưu ở `AppSettings.ServiceApiToken`).
- Thực thi phía server: middleware `RequireBearerToken` tại `wsl-docker-service/internal/httpserver/auth.go` so sánh `crypto/subtle.ConstantTimeCompare` với header `Authorization: Bearer <token>` hoặc header thay thế `X-DockLite-Token`. Trả `401` kèm `WWW-Authenticate: Bearer realm="docklite"` khi không khớp.
- Thực thi phía client (.NET):
  - REST: `src/DockLite.Core/Configuration/HttpClientAppSettings.cs::ApplyTo` gán `client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ...)`.
  - WebSocket: `HttpClientAppSettings.CopyAuthorizationToWebSocket` sao chép header Authorization sang `ClientWebSocket.Options.SetRequestHeader` và gắn thêm `X-Request-ID` (GUID mỗi phiên) — `src/DockLite.Infrastructure/Api/LogStreamClient.cs`, `src/DockLite.Infrastructure/Api/StatsStreamClient.cs`.
- Token lưu ở: `%LocalAppData%\DockLite\settings.json` (plain JSON; xem hướng dẫn `docs/docklite-api-token.md`).

**OAuth / IdP / SSO:**
- Không.

**Sigstore (chỉ trong CI/CD, không trong runtime):**
- Cosign keyless dùng OIDC của GitHub Actions để ký binary Go khi phát hành — `.github/workflows/release.yml` (job `release`, bước `cosign sign-blob`). Không ảnh hưởng runtime của app hay service.

## Monitoring & Observability

**Error Tracking:**
- Không dùng dịch vụ ngoài (không Sentry, không Application Insights). Exception phía WPF ghi vào `%LocalAppData%\DockLite\logs\` qua `src/DockLite.Core/Diagnostics/AppFileLog.cs` (ba hook trong `src/DockLite.App/App.xaml.cs`: `DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`).

**Analytics:**
- Không. Không có thư viện analytics/telemetry bên thứ ba. Các match text `telemetry` trong mã nguồn .NET đều là module chẩn đoán cục bộ (`DiagnosticTelemetry`), ghi file, không gửi mạng.

**Logs:**
- Service Go: `log/slog` và `log` chuẩn ghi ra stdout/stderr; middleware log request tại `wsl-docker-service/internal/httpserver/logging.go`; bộ đếm request đơn giản tại `wsl-docker-service/internal/httpserver/metrics.go` (endpoint `GET /api/metrics`, text/plain).
- Ứng dụng WPF: ghi file cục bộ (`docklite-*.log`, tùy chọn `docklite-diagnostic-*.log`). Không stream đến bất kỳ dịch vụ thu thập log bên ngoài nào.
- OpenTelemetry: có trong transitive dependencies của `github.com/docker/docker` (xem `go.sum`: `go.opentelemetry.io/otel v1.42.0`, `otlptracehttp v1.42.0`) nhưng service không cấu hình exporter OTLP; không phát telemetry ra ngoài.

## CI/CD & Deployment

**Hosting:**
- Không có hosting cloud — cả WPF và service Go chạy tại máy người dùng. WPF trên Windows, service Go trong WSL2 của chính máy đó.
- Phát hành: GitHub Releases (đính kèm binary Linux + SBOM + chữ ký Cosign). Người dùng tải xuống thủ công và chạy trong WSL.

**CI Pipeline:**
- GitHub Actions - Hai workflow:
  - `.github/workflows/ci.yml` - 3 job:
    - `dotnet` trên `windows-latest` với `actions/setup-dotnet@v4` (`8.0.x`), chạy `dotnet test DockLite.slnx -c Release`.
    - `go` trên `ubuntu-latest` với `actions/setup-go@v5` (phiên bản lấy từ `wsl-docker-service/go.mod`), chạy `go vet ./...`, `go test ./...`, `go build -o /tmp/docklite-wsl ./cmd/server`.
    - `go-integration` trên `ubuntu-latest` với Docker daemon sẵn có của runner, chạy `go test -tags=integration -count=1 -v ./integration/...`.
  - `.github/workflows/release.yml` - Trigger khi push tag `v*` (semver):
    - Build `docklite-wsl-linux-amd64` với `CGO_ENABLED=0`, `-trimpath -ldflags="-s -w"`.
    - Cài Syft qua script chính thức (`curl -sSfL https://raw.githubusercontent.com/anchore/syft/main/install.sh | sh -s -- -b /usr/local/bin`) và sinh SBOM CycloneDX JSON.
    - Sinh `checksums-sha256.txt` với `sha256sum`.
    - Ký keyless bằng Cosign 2.4.1 (`sigstore/cosign-installer@v3.7.0`) với OIDC GitHub (`permissions.id-token: write`).
    - Đẩy tất cả artifact lên GitHub Release qua `softprops/action-gh-release@v2`.
- Script build cục bộ:
  - Windows: `scripts/Publish-Wpf.ps1` (dotnet publish win-x64, self-contained single-file), `scripts/Build-GoInWsl.ps1` (gọi `wsl.exe bash -lc` chạy `bash scripts/build-server.sh`), `scripts/Start-DockLiteWsl.ps1` (gọi `wsl.exe bash -lc` chạy `bash scripts/run-server.sh`).
  - WSL: `wsl-docker-service/scripts/build-server.sh`, `run-server.sh`, `restart-server.sh`, `stop-server.sh`, `test-go.sh`, `test-integration.sh`.

## Environment Configuration

**Development:**
- Biến môi trường yêu cầu cho service Go:
  - `DOCKLITE_ADDR` (tùy chọn; mặc định `0.0.0.0:17890`) — `wsl-docker-service/cmd/server/main.go`.
  - `DOCKLITE_API_TOKEN` (tùy chọn; bật Bearer auth).
  - `DOCKLITE_WS_MAX_CONNECTIONS` (tùy chọn; 1–4096, mặc định 64) — `wsl-docker-service/internal/wslimit/wslimit.go`.
  - `DOCKER_HOST` / biến Docker chuẩn (tùy môi trường WSL).
- Cấu hình ứng dụng Windows: `%LocalAppData%\DockLite\settings.json` (các trường `ServiceBaseUrl`, `ServiceApiToken`, `HttpTimeoutSeconds`, `AutoStartWslService`, `WslDockerServiceWindowsPath`, `WslDockerServiceSyncSourceWindowsPath`, `WslDistribution`, `WslDockerServiceLinuxSyncPath`, `WslDockerServiceSyncDeleteExtra`, `WslDockerServiceSyncEnforceVersionGe`, `UiTimeZoneId`, `UiDateTimeFormat`, `UiTheme`, `UiLanguage`, `WslAutoStartHealthWaitSeconds`, `WslManualHealthWaitSeconds`, `HealthProbeSingleRequestSeconds`, `WslHealthPollIntervalMilliseconds`, `DiagnosticLocalTelemetryEnabled`, `ContainerStatsCpuWarnPercent`, `ContainerStatsMemoryWarnPercent`) — xem `src/DockLite.Core/Configuration/AppSettings.cs`.
- Mẫu env: `wsl-docker-service/.env.example` — sao chép thành `.env` trong cùng thư mục, `source` trước khi chạy binary; `.env` đã được gitignore.

**Staging:**
- Không áp dụng (không có hosted staging). Người dùng thay đổi cấu hình qua UI Cài đặt hoặc trực tiếp `settings.json`.

**Production:**
- Secret management: biến môi trường phía WSL (token API) và `settings.json` cục bộ (plain, quyền hệ điều hành là tường bảo vệ duy nhất). Không có vault/KMS.
- Redundancy/failover: không — service là single-node trong WSL của mỗi người dùng.

## IPC: WPF ↔ Go service

**Kênh truyền:** HTTP REST + WebSocket trên TCP loopback, không dùng named pipes hay gRPC.
- Service lắng nghe `0.0.0.0:17890` mặc định (hoặc theo `DOCKLITE_ADDR`) — `wsl-docker-service/cmd/server/main.go`. Phía client mặc định `http://127.0.0.1:17890/` (`src/DockLite.Core/Configuration/DockLiteDefaults.cs`); trường Settings cho phép đổi sang IP WSL qua nút **Điền IP WSL** (`wsl hostname -I`).
- HTTP client phía WPF:
  - `SocketsHttpHandler` với `UseProxy = false` để tránh proxy hệ thống chuyển hướng sai; base URL + timeout + Bearer token áp qua `HttpClientAppSettings.ApplyTo` — `src/DockLite.Infrastructure/Api/DockLiteHttpSession.cs`.
  - `RequestIdDelegatingHandler` gắn `X-Request-ID` (GUID) cho mọi request — `src/DockLite.Infrastructure/Api/RequestIdDelegatingHandler.cs`.
  - Retry đọc GET tối đa 3 lần khi lỗi mạng tạm thời — `src/DockLite.Infrastructure/Api/HttpReadRetry.cs`.
  - Envelope JSON thống nhất — `src/DockLite.Contracts/Api/ApiEnvelope.cs`, `ApiResult.cs`, `ApiErrorBody.cs`, `DockLiteErrorCodes.cs`.
- WebSocket client phía WPF: `System.Net.WebSockets.ClientWebSocket` tại `src/DockLite.Infrastructure/Api/LogStreamClient.cs` và `StatsStreamClient.cs`; upgrade từ `http://` sang `ws://` (hoặc `https://` → `wss://`).
- WebSocket server phía Go: `github.com/gorilla/websocket` với `CheckOrigin` cho phép mọi origin (vì chạy loopback), buffer 4 KiB, giới hạn đọc 1 MiB/tin nhắn, slot kết nối đồng thời — `wsl-docker-service/internal/wslimit/wslimit.go`. Stream log dùng `stdcopy.StdCopy` để tách stdout/stderr từ Docker multiplexed stream.

**Middleware HTTP phía server:** `httpserver.LogRequests` → `RequestContextTimeout` → `LimitRequestBody` (64 MiB POST, trừ `/api/images/load` có `MaxBytesReader` riêng 512 MiB) → (tùy chọn) `RequireBearerToken` → mux. Timeout: `ReadHeaderTimeout` 5s, `ReadTimeout`/`WriteTimeout` 30 phút (phục vụ load image lớn và compose dài), `IdleTimeout` 2 phút (xem `wsl-docker-service/internal/httpserver/limits.go`, `register.go`, `cmd/server/main.go`).

**Các route chính (đăng ký tại `wsl-docker-service/internal/httpserver/register.go`):**
- REST: `/api/openapi.json`, `/api/metrics`, `/api/health`, `/api/wsl/host-resources`, `/api/docker/info`, `/api/docker/events/stream` (NDJSON), `/api/containers`, `/api/containers/{id}/start|stop|restart|logs`, `/api/containers/stats-batch`, `/api/containers/top-by-cpu`, `/api/containers/top-by-memory`, `/api/images`, `/api/images/{id}` (inspect/history/export), `/api/images/pull`, `/api/images/pull/stream`, `/api/images/load`, `/api/images/remove`, `/api/images/prune`, `/api/images/trivy-scan`, `/api/networks`, `/api/volumes`, `/api/volumes/remove`, `/api/system/prune`, và cụm `/api/compose/*` (đăng ký trong `wsl-docker-service/internal/compose/compose.go::Register`).
- WebSocket: `/ws/containers/{id}/logs` (stream log, `wsl-docker-service/internal/ws/logs.go`), `/ws/containers/{id}/stats?intervalMs=500..5000` (stream stats, `wsl-docker-service/internal/docker/stats_ws.go`).

## WSL interop (WPF → wsl.exe)

- Ứng dụng WPF điều khiển WSL qua `wsl.exe` ngoài tiến trình (không dùng API WSL của Windows trực tiếp):
  - `src/DockLite.Infrastructure/Wsl/WslDockerServiceAutoStart.cs` - Tự khởi động/khởi động lại service trong WSL khi app mở.
  - `src/DockLite.Infrastructure/Wsl/WslDistroProbe.cs` - Liệt kê/kiểm tra distro.
  - `src/DockLite.Infrastructure/Wsl/WslHostAddressResolver.cs` - Lấy IP WSL bằng `wsl hostname -I`.
  - `src/DockLite.Infrastructure/Wsl/WslPathNormalizer.cs`, `WslPathProbe.cs` - Chuyển đổi đường dẫn Windows ↔ Unix qua `wslpath` (hỗ trợ đường dẫn UNC `\\wsl.localhost\<distro>\...`; app tự suy ra tên distro từ UNC).
  - `src/DockLite.Infrastructure/Wsl/WslDockerServicePathResolver.cs` - Tìm thư mục `wsl-docker-service` quanh vị trí exe (ngược 10 cấp).
- Lệnh thực thi phổ biến: `wsl.exe -d <distro> bash -lc "cd '<path>' && bash scripts/run-server.sh"` (và tương tự cho `restart-server.sh`, `build-server.sh`, `stop-server.sh`). Không cần token giữa WPF và `wsl.exe` — phân tách bảo vệ theo tài khoản Windows.

## Webhooks & Callbacks

**Incoming:**
- Không. Service không nhận webhook từ dịch vụ bên ngoài.

**Outgoing:**
- Không. Service không gửi webhook. Duy nhất lưu lượng rời máy là khi Docker Engine tự pull image hoặc Trivy cập nhật DB CVE — đều do công cụ bên dưới xử lý, không điều phối bởi DockLite.

---

*Integration audit: 2026-04-23*
