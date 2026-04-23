# Codebase Structure

**Analysis Date:** 2026-04-23

## Directory Layout

```
DockLite/
├── src/                                    # Mã nguồn .NET (giải pháp WPF)
│   ├── DockLite.App/                      # Project WPF (entry point, Views, ViewModels, UI services)
│   ├── DockLite.Core/                     # Domain thuần .NET (interface, config, diagnostics)
│   ├── DockLite.Contracts/                # DTO/envelope dùng chung với Go daemon
│   └── DockLite.Infrastructure/           # Cài đặt HTTP client, WSL helpers, settings store
├── wsl-docker-service/                    # Go daemon chạy trong WSL (docklite-wsl)
│   ├── cmd/server/                        # Entry main.go
│   ├── internal/                          # Package nội bộ theo domain
│   ├── integration/                       # Test tích hợp HTTP
│   ├── scripts/                           # Bash script build/run/restart/test
│   └── bin/                               # Binary build ra (chỉ trong WSL, không commit)
├── tests/
│   └── DockLite.Tests/                    # xUnit-style test cho .NET
├── scripts/                               # PowerShell cho host Windows
├── docs/                                  # Tài liệu người dùng
├── artifacts/                             # Output publish (.exe, self-contained)
├── .planning/                             # Sản phẩm GSD (phase, codebase map, state)
├── .cursor/                               # Cursor skills/agents/get-shit-done
├── DockLite.slnx                          # Solution format mới (XML rút gọn)
├── Directory.Build.props                  # Metadata build chung cho mọi project .NET
├── CHANGELOG.md                           # Nhật ký thay đổi
└── README.md                              # Hướng dẫn cài đặt và sử dụng
```

## Directory Purposes

**`src/DockLite.App/`:**
- Purpose: Project WPF (`net8.0-windows`, `UseWPF=true`, `OutputType=WinExe`), là composition root của ứng dụng.
- Contains: `App.xaml[.cs]`, `MainWindow.xaml[.cs]`, `AppHostContext.cs`, `AppShellFactory.cs` + `IAppShellFactory.cs`, `AppStartupCoordinator.cs`, `ServiceCollectionExtensions.cs`, `icon.ico`, `DockLite.App.csproj`.
- Key files:
  - `src/DockLite.App/App.xaml.cs` — Override `OnStartup` dựng DI, `OnExit` dispose.
  - `src/DockLite.App/ServiceCollectionExtensions.cs` — `AddDockLiteUi(...)` đăng ký toàn bộ singleton UI.
  - `src/DockLite.App/AppShellFactory.cs` — Tạo `ShellViewModel` và `ShellCompositionResult`.
  - `src/DockLite.App/AppStartupCoordinator.cs` — Điều phối auto-start WSL và refresh ban đầu.
  - `src/DockLite.App/MainWindow.xaml.cs` — Cập nhật `AppShellActivityState`, hủy `AppShutdownToken` khi đóng.
- Subdirectories:
  - `Views/` — File `.xaml` + code-behind per tab (`ContainersView`, `ImagesView`, `ComposeView`, `LogsView`, `DashboardView`, `SettingsView`, `CleanupView`, `NetworkVolumeView`, `DockerEventsView`, `AppDebugLogView`).
  - `ViewModels/` — ViewModel theo tab + `ShellViewModel`. Dùng `CommunityToolkit.Mvvm` (`ObservableObject`, `[RelayCommand]`). File lớn có thể chia `partial` (ví dụ `ContainersViewModel.BatchStats.cs`, `SettingsViewModel.WslCommands.cs`).
  - `Services/` — Screen APIs (`*ScreenApi` / `I*ScreenApi`), dialog/notification WPF, theme, i18n, activity state, health cache, shutdown token, error mapper.
  - `Models/` — Row/filter/option classes bind cho View (`SelectableContainerRow`, `ContainerListFilterOptions`, `LogLineViewModel`, ...).
  - `Converters/` — `IValueConverter` dùng trong XAML binding.
  - `Behaviors/` — Attached behavior (`MouseWheelScrollBubbling`).
  - `Themes/` — `ModernTheme.xaml`, `DarkTheme.xaml` (resource dictionary theme).
  - `Resources/` — `UiStrings.en.xaml`, `UiStrings.vi.xaml` cho i18n.
  - `Help/` — Hyperlink + text tooltip trang (`PageHelpTexts`, `LanSecurityDocPaths`).
  - `Compose/` — Template YAML mẫu cho tab Compose (`ComposeTemplateYaml.cs`).

**`src/DockLite.Core/`:**
- Purpose: Domain / contract .NET không phụ thuộc WPF hay HTTP cụ thể.
- Contains: `DockLite.Core.csproj` (`net8.0`), `DockLiteSourceVersion.cs` (version lấy từ assembly).
- Key files:
  - `src/DockLite.Core/Services/IDockLiteApiClient.cs` — Interface API client (mọi endpoint sidecar).
  - `src/DockLite.Core/Services/ILogStreamClient.cs`, `IStatsStreamClient.cs` — Interface streaming.
  - `src/DockLite.Core/Configuration/AppSettings.cs`, `AppSettingsDefaults.cs`, `IAppSettingsStore.cs`, `HttpClientAppSettings.cs`, `ServiceBaseUriHelper.cs`, `DockLiteDefaults.cs`.
  - `src/DockLite.Core/Diagnostics/AppFileLog.cs`, `DiagnosticTelemetry.cs`.
  - `src/DockLite.Core/Compose/ComposeComposePaths.cs` — Chuẩn hoá đường dẫn file compose giữa Windows và WSL.

**`src/DockLite.Contracts/`:**
- Purpose: Kiểu dữ liệu «wire» dùng chung giữa WPF và Go daemon.
- Contains: `DockLite.Contracts.csproj` (`net8.0`, không reference project khác).
- Key files (tất cả trong `src/DockLite.Contracts/Api/`):
  - `ApiEnvelope.cs`, `ApiResult.cs`, `ApiErrorBody.cs`, `ApiEnvelopeExtensions.cs`, `DockLiteErrorCodes.cs`, `HealthResponse.cs`, `EmptyApiPayload.cs`.
  - DTO container: `ContainerSummaryDto.cs`, `ContainerListData.cs`, `ContainerInspectData.cs`, `ContainerLogsData.cs`, `ContainerStatsSnapshotData.cs`, `ContainerStatsBatchData.cs`, `ContainerTopMemoryData.cs`.
  - DTO image: `ImageSummaryDto.cs`, `ImageListData.cs`, `ImageHistoryData.cs`, `ImageHistoryLayerDto.cs`, `ImageInspectData.cs`, `ImagePullRequest.cs`, `ImagePullResultData.cs`, `ImageLoadResultData.cs`, `ImagePruneRequest.cs`, `ImageRemoveRequest.cs`, `ImageTrivyScanRequest.cs`, `ImageTrivyScanResultData.cs`.
  - DTO compose: `ComposeCommandData.cs`, `ComposeIdRequest.cs`, `ComposeProjectAddData.cs`, `ComposeProjectAddRequest.cs`, `ComposeProjectDto.cs`, `ComposeProjectListData.cs`, `ComposeProjectPatchData.cs`, `ComposeProjectPatchRequest.cs`, `ComposeServiceExecRequest.cs`, `ComposeServiceListData.cs`, `ComposeServiceLogsRequest.cs`, `ComposeServiceRequest.cs`.
  - DTO network/volume/system: `NetworkListData.cs`, `NetworkSummaryDto.cs`, `VolumeListData.cs`, `VolumeSummaryDto.cs`, `VolumeRemoveRequest.cs`, `SystemPruneRequest.cs`, `DockerInfoData.cs`, `WslHostResourcesData.cs`.

**`src/DockLite.Infrastructure/`:**
- Purpose: Cài đặt chi tiết cho Core (HTTP, streaming, lưu cài đặt, tương tác WSL bằng `wsl.exe`).
- Contains: `DockLite.Infrastructure.csproj` (`net8.0`, reference Core + Contracts).
- Key files:
  - `Api/DockLiteApiClient.cs` — Cài đặt `IDockLiteApiClient`.
  - `Api/DockLiteHttpSession.cs` — Hot-swappable `HttpClient`.
  - `Api/RequestIdDelegatingHandler.cs` — Sinh `X-Request-Id` cho trace.
  - `Api/HttpReadRetry.cs` — Retry đọc body khi WSL TCP reset.
  - `Api/LogStreamClient.cs`, `Api/StatsStreamClient.cs` — WebSocket client.
  - `Configuration/AppSettingsStore.cs` — Persist `AppSettings` ra đĩa (LocalAppData).
  - `Configuration/DockLiteApiDefaults.cs` — Hằng base URL/port mặc định.
  - `Wsl/WslDockerServiceAutoStart.cs` — 1063 dòng logic auto-start + recovery; điểm vào `TryEnsureRunningAsync`.
  - `Wsl/WslDistroProbe.cs`, `WslPathNormalizer.cs`, `WslPathProbe.cs`, `WslHostAddressResolver.cs`, `WslDockerServicePathResolver.cs`, `WslStartupProgress.cs`, `WslEnsureFailureReason.cs`.

**`wsl-docker-service/`:**
- Purpose: Go daemon nghe HTTP trên `0.0.0.0:17890` (override `DOCKLITE_ADDR`), bao quanh Docker Engine API.
- Contains: `go.mod`, `go.sum`, `.env.example`, `_file.editorconfig`, `_file.gitattributes`.
- Key files:
  - `wsl-docker-service/cmd/server/main.go` — Entry daemon (mux + middleware chain).
  - `wsl-docker-service/internal/httpserver/register.go` — Bảng route.
  - `wsl-docker-service/internal/httpserver/context_timeout.go` — Deadline theo path.
  - `wsl-docker-service/internal/dockerengine/client.go` — Singleton Docker client.
  - `wsl-docker-service/internal/docker/*.go` — Handler từng nhóm (containers, images, volumes, networks, events, stats, health, system prune, trivy scan).
  - `wsl-docker-service/internal/compose/compose.go` (+ `compose_services.go`, `compose_profiles.go`, `compose_docker_args.go`, `compose_cache.go`) — Xử lý `docker compose`.
  - `wsl-docker-service/internal/ws/{containers.go, logs.go}` — WebSocket upgrade.
  - `wsl-docker-service/internal/hostresources/hostresources.go` — Thông tin host Linux.
  - `wsl-docker-service/internal/apiresponse/apiresponse.go` — Envelope mirror.
  - `wsl-docker-service/internal/appversion/{appversion.go, VERSION}` — Version cho `/api/health`.
  - `wsl-docker-service/integration/api_integration_test.go` — Test tích hợp HTTP.
  - `wsl-docker-service/scripts/{run-server.sh, restart-server.sh, build-server.sh, stop-server.sh, test-go.sh, test-integration.sh, normalize-sh-lf.ps1}`.
- Subdirectories: `bin/` (binary build, không commit), `cmd/` (entry), `internal/` (package riêng), `integration/` (test), `scripts/` (bash + 1 PowerShell chuẩn hoá line ending).

**`tests/DockLite.Tests/`:**
- Purpose: Test .NET (xUnit) cho contract/envelope/helper.
- Contains: `DockLite.Tests.csproj`, các file `*Tests.cs`.
- Key files: `ApiEnvelopeJsonTests.cs`, `ApiResultEnvelopeTests.cs`, `AppSettingsDefaultsTests.cs`, `ComposeComposePathsTests.cs`, `DockLiteSourceVersionTests.cs`, `HealthResponseContractTests.cs`, `NetworkErrorMessageMapperTests.cs`, `WslPathNormalizerTests.cs`.

**`scripts/`:**
- Purpose: PowerShell automation trên host Windows.
- Contains:
  - `Start-DockLiteWsl.ps1` — Gọi `wsl.exe bash -lc "cd <wsl-path> && exec bash scripts/run-server.sh"` (dev thủ công).
  - `Build-GoInWsl.ps1` — Gọi WSL build binary Go (`scripts/build-server.sh`).
  - `Publish-Wpf.ps1` — `dotnet publish` ra `artifacts/`.

**`docs/`:**
- Purpose: Tài liệu mở rộng cho người dùng cuối (bảo mật LAN, troubleshooting).

**`artifacts/`:**
- Purpose: Output của `scripts/Publish-Wpf.ps1` (exe, dependencies). Không commit.

**`.planning/`:**
- Purpose: Thư mục GSD — milestone, phase, codebase map, state.
- Subdirectories: `codebase/` (các file map này), các phase directory khi có.

**`.cursor/`:**
- Purpose: Tài nguyên cho Cursor (skills, agents, workflow get-shit-done, template, reference).

## Key File Locations

**Entry Points:**
- `src/DockLite.App/App.xaml.cs` — WPF startup (`App.OnStartup`).
- `src/DockLite.App/MainWindow.xaml.cs` — Constructor + `Loaded` trigger startup coordinator.
- `src/DockLite.App/AppStartupCoordinator.cs` — Điều phối khởi động (WSL ensure + dashboard refresh).
- `wsl-docker-service/cmd/server/main.go` — Entry Go daemon.
- `wsl-docker-service/scripts/run-server.sh` — Launcher binary trong WSL.
- `wsl-docker-service/scripts/restart-server.sh` — Kill + rerun.
- `scripts/Start-DockLiteWsl.ps1` — Launcher thủ công từ Windows.

**Configuration:**
- `DockLite.slnx` — Solution (format XML mới của `dotnet`).
- `Directory.Build.props` — Metadata build (Version, Company, Product) cho mọi project .NET.
- `src/DockLite.App/DockLite.App.csproj`, `src/DockLite.Core/DockLite.Core.csproj`, `src/DockLite.Contracts/DockLite.Contracts.csproj`, `src/DockLite.Infrastructure/DockLite.Infrastructure.csproj`, `tests/DockLite.Tests/DockLite.Tests.csproj` — Project .NET (TargetFramework, PackageReference).
- `src/DockLite.Core/Configuration/AppSettings.cs` + `AppSettingsDefaults.cs` — Cấu hình người dùng (serialize JSON).
- `wsl-docker-service/go.mod`, `go.sum` — Module Go.
- `wsl-docker-service/.env.example` — Biến môi trường mẫu (`DOCKLITE_ADDR`, `DOCKLITE_API_TOKEN`).
- `wsl-docker-service/internal/httpserver/openapi.json` — Hợp đồng API phát lại qua `/api/openapi.json`.
- `.gitignore`, `_file.gitignore` — Loại trừ.

**Core Logic:**
- `src/DockLite.Infrastructure/Api/DockLiteApiClient.cs` — Toàn bộ gọi HTTP từ WPF sang sidecar.
- `src/DockLite.Infrastructure/Wsl/WslDockerServiceAutoStart.cs` — Auto-start sidecar WSL + recovery.
- `src/DockLite.App/AppShellFactory.cs` — Compose shell ViewModel + HTTP session + health cache.
- `src/DockLite.App/ViewModels/ShellViewModel.cs` — Điều phối tab, header service status.
- `wsl-docker-service/internal/httpserver/register.go` — Bảng route HTTP/WebSocket.
- `wsl-docker-service/internal/docker/containers.go`, `images.go`, `compose/compose.go` — Business logic chính phía Go.
- `wsl-docker-service/internal/dockerengine/client.go` — Singleton client Docker Engine.

**Testing:**
- `tests/DockLite.Tests/` — Unit test contract/helper .NET.
- `wsl-docker-service/integration/api_integration_test.go` — Test tích hợp HTTP Go.
- `wsl-docker-service/internal/httpserver/openapi_test.go`, `wsl-docker-service/internal/wslimit/wslimit_test.go` — Test Go nội bộ.
- `wsl-docker-service/scripts/test-go.sh`, `scripts/test-integration.sh` — Lệnh chạy test.

**Documentation:**
- `README.md` — Hướng dẫn cài đặt, yêu cầu WSL/Docker, build.
- `CHANGELOG.md` — Lịch sử thay đổi.
- `docs/` — Tài liệu chuyên đề.
- `src/DockLite.App/Help/PageHelpTexts.cs` + `LanSecurityDocPaths.cs` — Help in-app.

## Naming Conventions

**Files (.NET):**
- `PascalCase.cs` cho mọi class/record/enum. Một file = một tên public type chính (khớp tên file).
- `PascalCase.xaml` + `PascalCase.xaml.cs` cho View.
- Partial class chia theo chức năng: `ContainersViewModel.cs` + `ContainersViewModel.BatchStats.cs`, `SettingsViewModel.cs` + `SettingsViewModel.WslCommands.cs`.
- Interface bắt đầu bằng `I`: `IDockLiteApiClient`, `IContainerScreenApi`, `IAppShellFactory`, `IAppShutdownToken`.
- ViewModel suffix `ViewModel`: `DashboardViewModel`, `ShellViewModel`.
- Adapter UI suffix `ScreenApi`: `ContainerScreenApi`, `ImageScreenApi`.
- DTO suffix `Dto`, request suffix `Request`, payload envelope suffix `Data`: `ContainerSummaryDto`, `ImagePullRequest`, `ContainerListData`.
- Test suffix `Tests`: `ApiEnvelopeJsonTests.cs`.
- XAML resource dictionary: `PascalCase.xaml` — `ModernTheme.xaml`, `DarkTheme.xaml`, `UiStrings.{lang}.xaml` (ví dụ `UiStrings.vi.xaml`).

**Files (Go):**
- `snake_case.go` cho file thường (`containers.go`, `events_stream.go`, `container_stats_batch.go`, `image_trivy_scan.go`, `compose_services.go`, `compose_docker_args.go`).
- Test: `<file>_test.go` (`openapi_test.go`, `wslimit_test.go`, `api_integration_test.go`).
- Package = tên thư mục, chữ thường, không dấu gạch dưới: `httpserver`, `dockerengine`, `dockercli`, `apiresponse`, `hostresources`, `appversion`, `wslimit`, `ws`, `compose`, `docker`.

**Bash/PowerShell scripts:**
- Bash dùng `kebab-case.sh`: `run-server.sh`, `restart-server.sh`, `build-server.sh`, `test-integration.sh`.
- PowerShell dùng Verb-Noun PascalCase: `Start-DockLiteWsl.ps1`, `Publish-Wpf.ps1`, `Build-GoInWsl.ps1`.

**Directories:**
- .NET: PascalCase (`ViewModels`, `Services`, `Views`, `Themes`, `Resources`, `Converters`, `Behaviors`, `Models`, `Help`, `Compose`, `Configuration`, `Api`, `Wsl`, `Diagnostics`).
- Go/Scripts: lowercase (`cmd`, `internal`, `integration`, `bin`, `scripts`).
- Giải pháp/project: `DockLite.<Layer>` (dấu chấm là namespace, không phải dấu gạch).

**Namespace .NET:** Gương theo thư mục — `DockLite.App.ViewModels`, `DockLite.App.Services`, `DockLite.Core.Services`, `DockLite.Core.Configuration`, `DockLite.Infrastructure.Api`, `DockLite.Infrastructure.Wsl`, `DockLite.Contracts.Api`.

**Special Patterns:**
- `_file.<something>` ở gốc repo/`wsl-docker-service` (ví dụ `_file.gitignore`, `_file.gitattributes`, `_file.editorconfig`) — bản backup/template, không phải file thật đang được git dùng.
- `AssemblyInfo.cs` trong `DockLite.App` giữ attribute theme/XAML.
- `i18n key`: chuỗi tài nguyên XAML dùng khoá `Ui_<Page>_<Element>` (`Ui_MainWindow_Title`, xem `App.xaml.cs`).

## Where to Add New Code

**Endpoint mới (REST hoặc WebSocket) ở sidecar:**
- Handler Go: thêm file trong `wsl-docker-service/internal/docker/` (hoặc `compose/`, `ws/` tùy domain). Tên file `snake_case.go`, hàm handler exported PascalCase.
- Đăng ký route: thêm dòng `mux.HandleFunc("/api/...", pkg.Handler)` vào `wsl-docker-service/internal/httpserver/register.go`. Với compose, thêm vào `compose.Register(mux)` (`wsl-docker-service/internal/compose/compose.go`).
- Nếu cần deadline khác mặc định: cập nhật `timeoutForRequest` trong `wsl-docker-service/internal/httpserver/context_timeout.go`.
- Tài liệu OpenAPI: cập nhật `wsl-docker-service/internal/httpserver/openapi.json`.
- DTO chia sẻ: thêm record C# vào `src/DockLite.Contracts/Api/` và struct Go tương ứng (thủ công).
- Client .NET: thêm method vào `src/DockLite.Core/Services/IDockLiteApiClient.cs` và implement trong `src/DockLite.Infrastructure/Api/DockLiteApiClient.cs`.
- Integration test: thêm case vào `wsl-docker-service/integration/api_integration_test.go`.

**Tab / trang mới trong WPF:**
- View XAML: `src/DockLite.App/Views/<Name>View.xaml` + code-behind `<Name>View.xaml.cs` (chỉ `InitializeComponent`).
- ViewModel: `src/DockLite.App/ViewModels/<Name>ViewModel.cs`, kế thừa `ObservableObject`, dùng `[RelayCommand]`.
- Screen API: `src/DockLite.App/Services/I<Name>ScreenApi.cs` + `<Name>ScreenApi.cs` (wrap `IDockLiteApiClient`, validate qua `ScreenApiInputValidation`, map lỗi qua `NetworkErrorMessageMapper`).
- Compose: thêm `Lazy<<Name>ViewModel>` vào `src/DockLite.App/AppShellFactory.cs` và truyền vào `ShellViewModel` (cập nhật cả constructor `ShellViewModel` trong `src/DockLite.App/ViewModels/ShellViewModel.cs`).
- Navigation/sidebar: bổ sung lệnh điều hướng trong `ShellViewModel` và mục trong `MainWindow.xaml`.
- Cờ hoạt động (nếu có polling): thêm property `Is<Page>PageVisible` / `ShouldPoll...` trong `src/DockLite.App/Services/AppShellActivityState.cs`.
- i18n: thêm key vào `src/DockLite.App/Resources/UiStrings.en.xaml` và `UiStrings.vi.xaml`.
- Help text (nếu có): cập nhật `src/DockLite.App/Help/PageHelpTexts.cs`.

**Cấu hình người dùng mới:**
- Property: thêm vào `src/DockLite.Core/Configuration/AppSettings.cs` + default trong `AppSettingsDefaults.cs`.
- Persist: `AppSettingsStore` (`src/DockLite.Infrastructure/Configuration/AppSettingsStore.cs`) tự serialize; không cần sửa trừ khi format đổi.
- UI: thêm binding trong `SettingsViewModel` (`src/DockLite.App/ViewModels/SettingsViewModel.cs` hoặc partial `SettingsViewModel.WslCommands.cs`) + `Views/SettingsView.xaml`.
- Áp dụng runtime: nếu ảnh hưởng HTTP, cập nhật `HttpClientAppSettings.ApplyTo` trong `src/DockLite.Core/Configuration/HttpClientAppSettings.cs` và gọi `DockLiteHttpSession.Reconfigure` khi Lưu.

**Converter / Behavior WPF:**
- Converter: `src/DockLite.App/Converters/<Name>Converter.cs` implement `IValueConverter`; khai báo resource trong `Themes/ModernTheme.xaml` hoặc view local.
- Behavior: `src/DockLite.App/Behaviors/<Name>.cs` (attached property).

**Model row/option cho View:**
- `src/DockLite.App/Models/<Name>.cs` (ví dụ `SelectableContainerRow`, `ImageListFilterOptions`).

**Helper Core thuần:**
- `src/DockLite.Core/<SubArea>/<Name>.cs` (thêm subfolder nếu cần nhóm lại, ví dụ `Compose/`, `Diagnostics/`).
- Không reference WPF hay HTTP cụ thể.

**Adapter hạ tầng mới (VD interact với OS/file/network):**
- `src/DockLite.Infrastructure/<Area>/<Name>.cs`. Nhóm theo domain: `Api/`, `Wsl/`, `Configuration/` (tạo thêm folder nếu domain mới).

**Go helper/middleware mới:**
- Middleware HTTP: thêm vào `wsl-docker-service/internal/httpserver/` với tên file mô tả (`logging.go`, `auth.go`). Compose trong `cmd/server/main.go` theo thứ tự outer→inner.
- Shared helper Docker: `wsl-docker-service/internal/docker/<name>.go` hoặc thêm package mới dưới `internal/` nếu tái sử dụng rộng.

**Test:**
- Unit .NET: `tests/DockLite.Tests/<Subject>Tests.cs` (xUnit, `[Fact]` / `[Theory]`).
- Unit Go: đặt cạnh file `<file>_test.go` trong cùng package.
- Integration Go: thêm case vào `wsl-docker-service/integration/api_integration_test.go`, chạy qua `wsl-docker-service/scripts/test-integration.sh`.

**Script automation:**
- Windows: `scripts/<Verb>-<Noun>.ps1` (PowerShell approved verbs).
- WSL: `wsl-docker-service/scripts/<kebab-name>.sh`. Giữ line ending LF — có sẵn `normalize-sh-lf.ps1` để chuẩn hoá.

## Special Directories

**`wsl-docker-service/bin/`:**
- Purpose: Binary `docklite-wsl` build trong WSL (`scripts/build-server.sh`).
- Source: Tự build trong WSL; không commit (ignore qua `.gitignore`).
- Committed: Không.

**`artifacts/`:**
- Purpose: Output `dotnet publish` từ `scripts/Publish-Wpf.ps1` (self-contained Windows).
- Source: Build script.
- Committed: Không.

**`src/*/bin/`, `src/*/obj/`, `tests/*/bin/`, `tests/*/obj/`:**
- Purpose: Output trung gian `dotnet build`.
- Source: Tự sinh.
- Committed: Không.

**`.planning/`:**
- Purpose: Sản phẩm quy trình GSD (milestone, phase, codebase map, state, retrospective).
- Source: Lệnh `/gsd-*` / skill GSD tạo ra.
- Committed: Có (là nguồn sự thật quy trình).

**`.cursor/`:**
- Purpose: Skills/agents/get-shit-done templates cho Cursor.
- Source: Cài đặt GSD.
- Committed: Có.

**`_file.gitignore`, `_file.gitattributes`, `_file.editorconfig`:**
- Purpose: Bản backup/template — file thực là `.gitignore` / `.gitattributes` / `.editorconfig` (dot-file) tương ứng. Prefix `_file.` tránh editor ẩn.
- Committed: Có (dùng để khôi phục khi dot-file bị xoá).

---

*Structure analysis: 2026-04-23*
