# Technology Stack

**Analysis Date:** 2026-04-23

## Languages

**Primary:**
- C# (C# 12, mặc định của .NET 8) - Toàn bộ ứng dụng WPF, Core, Contracts, Infrastructure và test .NET (thư mục `src/` và `tests/`).
- Go 1.25.0 - Service chạy trong WSL2 (thư mục `wsl-docker-service/`); khai báo tại `wsl-docker-service/go.mod`.

**Secondary:**
- XAML - Giao diện WPF (ví dụ `src/DockLite.App/MainWindow.xaml`, `src/DockLite.App/Views/*.xaml`, `src/DockLite.App/Themes/`, `src/DockLite.App/Resources/UiStrings.vi.xaml`).
- PowerShell - Script build/publish phía Windows (`scripts/Publish-Wpf.ps1`, `scripts/Build-GoInWsl.ps1`, `scripts/Start-DockLiteWsl.ps1`, `wsl-docker-service/scripts/normalize-sh-lf.ps1`).
- Bash - Script vận hành service Go trong WSL (`wsl-docker-service/scripts/build-server.sh`, `run-server.sh`, `restart-server.sh`, `stop-server.sh`, `test-go.sh`, `test-integration.sh`).
- YAML - Pipeline CI/CD (`.github/workflows/ci.yml`, `.github/workflows/release.yml`).

## Runtime

**Environment:**
- Phía Windows: .NET 8 Desktop Runtime (WPF).
  - `src/DockLite.App/DockLite.App.csproj` khai báo `<TargetFramework>net8.0-windows</TargetFramework>`, `<UseWPF>true</UseWPF>`, `<OutputType>WinExe</OutputType>`.
  - Các dự án phụ: `src/DockLite.Core/DockLite.Core.csproj`, `src/DockLite.Contracts/DockLite.Contracts.csproj` dùng `net8.0`; `src/DockLite.Infrastructure/DockLite.Infrastructure.csproj` dùng `net8.0`.
  - Dự án test: `tests/DockLite.Tests/DockLite.Tests.csproj` dùng `net8.0-windows` (`<UseWPF>false</UseWPF>` nên chạy headless).
  - Publish self-contained mặc định (không cần .NET runtime trên máy đích): `scripts/Publish-Wpf.ps1` cấu hình `-r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true`.
- Phía WSL: Go toolchain 1.25+ để build (`go.mod` khai báo `go 1.25.0`); binary phát hành là `linux/amd64` với `CGO_ENABLED=0` (`.github/workflows/release.yml`).
- Yêu cầu ngoài: Windows 10/11 có WSL2 đã cài, `wsl.exe` trong `PATH`; trong distro cần Docker Engine (`docker`, `docker compose`) trong `PATH`. Service Go lắng nghe mặc định `0.0.0.0:17890` (ghi đè bằng biến môi trường `DOCKLITE_ADDR`) — xem `wsl-docker-service/cmd/server/main.go` và `wsl-docker-service/.env.example`.

**Package Manager:**
- NuGet (phía .NET) — khôi phục qua `dotnet restore` / `dotnet build DockLite.slnx`. Không có lockfile (`packages.lock.json`) trong repo.
- Go modules (phía WSL) — lockfile `wsl-docker-service/go.sum` có mặt cùng `wsl-docker-service/go.mod`.

## Frameworks

**Core:**
- WPF (Windows Presentation Foundation) - UI cho `DockLite.App` (`<UseWPF>true</UseWPF>` trong `src/DockLite.App/DockLite.App.csproj`; entry `App.xaml` / `src/DockLite.App/App.xaml.cs`; `MainWindow.xaml` + các view trong `src/DockLite.App/Views/`).
- CommunityToolkit.Mvvm 8.4.0 - Khung MVVM (observable/relay commands) cho toàn bộ ViewModel trong `src/DockLite.App/ViewModels/`.
- Microsoft.Extensions.DependencyInjection 8.0.1 - DI container dùng ở `src/DockLite.App/ServiceCollectionExtensions.cs`, khởi tạo tại `src/DockLite.App/App.xaml.cs`.
- net/http (thư viện chuẩn Go) - HTTP server ở `wsl-docker-service/cmd/server/main.go`; middleware ở `wsl-docker-service/internal/httpserver/` (`register.go`, `auth.go`, `logging.go`, `limits.go`, `context_timeout.go`).

**Testing:**
- xUnit 2.9.3 - Khung test .NET (`tests/DockLite.Tests/DockLite.Tests.csproj`, global `using Xunit`).
- Microsoft.NET.Test.Sdk 17.14.1 - Runner test của .NET.
- xunit.runner.visualstudio 3.1.4 - Adapter test cho Visual Studio / `dotnet test`.
- coverlet.collector 6.0.4 - Thu thập độ phủ mã cho test .NET.
- `testing` (thư viện chuẩn Go) + `go test ./...` - Test Go trong `wsl-docker-service/internal/**/*_test.go` và test tích hợp `wsl-docker-service/integration/api_integration_test.go` (build tag `integration`, chạy qua `go test -tags=integration ./integration/...`).

**Build/Dev:**
- .NET SDK 8 (`actions/setup-dotnet@v4` với `dotnet-version: "8.0.x"` trong `.github/workflows/ci.yml`).
- Go 1.25 toolchain (`actions/setup-go@v5` với `go-version-file: wsl-docker-service/go.mod`).
- `dotnet publish` (gộp single-file) qua `scripts/Publish-Wpf.ps1`.
- `go build -trimpath -ldflags="-s -w"` (`.github/workflows/release.yml`) và `wsl-docker-service/scripts/build-server.sh`.
- Syft (cài từ script chính thức của Anchore) - Sinh SBOM CycloneDX JSON cho binary Go trong workflow Release.
- Sigstore Cosign 2.4.1 (`sigstore/cosign-installer@v3.7.0`) - Ký keyless (OIDC GitHub) binary Go trong workflow Release.
- `actions/checkout@v4`, `softprops/action-gh-release@v2` - Các action tạo release và đính kèm artifact.

## Key Dependencies

**Critical:**
- github.com/docker/docker v26.1.5+incompatible (`wsl-docker-service/go.mod`) - SDK Docker Engine Go; hầu hết thao tác gọi trực tiếp qua client thay vì spawn `docker` CLI (xem `wsl-docker-service/internal/dockerengine/client.go`, các handler trong `wsl-docker-service/internal/docker/`).
- github.com/gorilla/websocket v1.5.3 (`wsl-docker-service/go.mod`) - WebSocket upgrader cho stream log và stats container (`wsl-docker-service/internal/ws/logs.go`, `wsl-docker-service/internal/docker/stats_ws.go`, `wsl-docker-service/internal/wslimit/wslimit.go`).
- github.com/docker/go-units v0.5.0 (`wsl-docker-service/go.mod`) - Định dạng kích thước/đơn vị trong API image và container.
- CommunityToolkit.Mvvm 8.4.0 (`src/DockLite.App/DockLite.App.csproj`) - `ObservableObject`, `RelayCommand` dùng xuyên suốt ViewModel.
- Microsoft.Extensions.DependencyInjection 8.0.1 (`src/DockLite.App/DockLite.App.csproj`) - Mạng lưới DI cho service, API client, ViewModel.

**Infrastructure:**
- `System.Net.Http.HttpClient` + `System.Net.Http.Json` (BCL .NET) - Client REST tới service Go; cấu hình qua `src/DockLite.Infrastructure/Api/DockLiteHttpSession.cs` (tắt proxy hệ thống, `SocketsHttpHandler`, handler custom `RequestIdDelegatingHandler`).
- `System.Net.WebSockets.ClientWebSocket` (BCL .NET) - Client WebSocket tới service Go (log và stats) ở `src/DockLite.Infrastructure/Api/LogStreamClient.cs`, `src/DockLite.Infrastructure/Api/StatsStreamClient.cs`.
- `System.Text.Json` (BCL .NET) - Parser JSON cho envelope API và cấu hình `settings.json`.
- github.com/docker/docker/pkg/stdcopy - Gỡ kênh stdout/stderr của Docker multiplexed log stream (`wsl-docker-service/internal/ws/logs.go`).
- go.opentelemetry.io/otel v1.42.0 (indirect, kéo theo từ SDK `docker/docker`) - Có trong `go.sum` nhưng service không cấu hình exporter; thực chất không phát telemetry ra ngoài.

## Configuration

**Environment:**
- Service Go đọc biến môi trường:
  - `DOCKLITE_ADDR` - địa chỉ lắng nghe (mặc định `0.0.0.0:17890`) — `wsl-docker-service/cmd/server/main.go`.
  - `DOCKLITE_API_TOKEN` - token tùy chọn, bật xác thực Bearer cho mọi REST/WebSocket — `wsl-docker-service/internal/httpserver/auth.go`.
  - `DOCKLITE_WS_MAX_CONNECTIONS` - giới hạn kết nối WebSocket đồng thời (mặc định 64, tối đa 4096) — `wsl-docker-service/internal/wslimit/wslimit.go`.
  - `DOCKER_HOST` / biến Docker chuẩn — được đọc qua `client.FromEnv` trong `wsl-docker-service/internal/dockerengine/client.go`.
- Mẫu biến môi trường: `wsl-docker-service/.env.example` (không commit `.env` — xem `.gitignore` dòng `wsl-docker-service/.env`).
- Ứng dụng WPF không đọc biến môi trường cho cấu hình nghiệp vụ; toàn bộ cấu hình nằm trong file JSON (xem dưới).

**Build:**
- `DockLite.slnx` - Định dạng solution mới, tham chiếu 4 project `src/*` + `tests/DockLite.Tests`.
- `Directory.Build.props` - Đặt `Version`, `AssemblyVersion`, `FileVersion` (hiện tại `0.1.0`), `Product`, `Company`, `Copyright` cho mọi project C#.
- `src/DockLite.App/DockLite.App.csproj` - Bật WPF, chỉ định icon `icon.ico`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`.
- `tests/DockLite.Tests/DockLite.Tests.csproj` - `IsPackable=false`, global `Using Xunit`.
- `wsl-docker-service/go.mod` / `wsl-docker-service/go.sum` - Khóa phiên bản module Go.
- `wsl-docker-service/internal/appversion/VERSION` - Chuỗi phiên bản nhúng vào binary Go qua `//go:embed` (`wsl-docker-service/internal/appversion/appversion.go`), trả về trường `version` trong `GET /api/health`.
- `wsl-docker-service/_file.editorconfig` và `_file.gitattributes` - File template tên bắt đầu bằng `_file.` (được đổi tên khi áp dụng); giữ định dạng LF cho script shell.
- Cấu hình ứng dụng khi chạy: `%LocalAppData%\DockLite\settings.json` (đọc/ghi bởi `src/DockLite.Infrastructure/Configuration/AppSettingsStore.cs`).

## Platform Requirements

**Development:**
- Windows 10/11 cho ứng dụng WPF (target `net8.0-windows`); cài .NET SDK 8.
- WSL2 (khuyến nghị Ubuntu) với Go 1.25+ và Docker Engine + `docker compose` trong `PATH` (xem phần service Go trong `README.md`).
- Tùy chọn: `trivy` trong `PATH` của WSL để kích hoạt endpoint `POST /api/images/trivy-scan` (`wsl-docker-service/internal/docker/image_trivy_scan.go`); `rsync` trong distro để đồng bộ mã có xóa file thừa.
- Git, `wsl.exe` trong `PATH` trên Windows để script PowerShell `scripts/*.ps1` gọi xuống WSL.

**Production:**
- Ứng dụng WPF: phát hành file `DockLite.App.exe` (self-contained, win-x64, single-file) vào `artifacts/publish-win-x64/` — `scripts/Publish-Wpf.ps1`. Tùy chọn framework-dependent yêu cầu cài .NET 8 Desktop Runtime (win-x64).
- Service Go: binary `docklite-wsl-linux-amd64` (Linux amd64) phát hành qua GitHub Release (workflow `.github/workflows/release.yml`) khi push tag `v*`; kèm SBOM CycloneDX (`sbom-docklite-wsl.cdx.json`), `checksums-sha256.txt`, chữ ký Cosign keyless (`*.sig`, `*.cosign.bundle`). Binary thường được build trong WSL tại `wsl-docker-service/bin/docklite-wsl` qua `wsl-docker-service/scripts/build-server.sh`.
- Docker Engine phải sẵn sàng trong WSL distro được cấu hình (DockLite không đóng gói Docker).

---

*Stack analysis: 2026-04-23*
