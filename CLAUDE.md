# DockLite — ngữ cảnh dự án (GSD + Cursor)

Nguồn đồng bộ: `.planning/PROJECT.md`, `gsd-sdk query generate-claude-md` có thể ghi đè theo section marker khi cần.

<!-- gsd-project-start source:PROJECT.md -->
## Project

**DockLite** — ứng dụng desktop Windows (WPF + .NET 8) quản lý Docker chạy trong WSL qua sidecar Go (`docklite-wsl`). WPF là thin client MVVM; mọi thao tác Docker đi qua HTTP REST + WebSocket tới sidecar. Sidecar wrap Docker Engine API local socket.

**Core value (milestone v2.0):** DockLite phải chạy an toàn mặc định theo Zero-Trust. IPC giữa WPF và Go service là kênh tin cậy ngay cả khi user opt-in mở LAN.

**Current milestone:** v2.0 IPC Hardening — 5 phase, 13 plan. Chi tiết ở `.planning/ROADMAP.md`.

Đọc chi tiết đầy đủ tại `.planning/PROJECT.md`.
<!-- gsd-project-end -->

<!-- gsd-stack-start source:codebase/STACK.md -->
## Technology Stack

- **Windows side:** C# 12 / .NET 8 WPF (`src/DockLite.App`, `src/DockLite.Core`, `src/DockLite.Contracts`, `src/DockLite.Infrastructure`), `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection`, xUnit + FluentAssertions (`tests/DockLite.Tests`)
- **WSL side:** Go 1.25, `github.com/docker/docker v26.1.5+incompatible`, `github.com/gorilla/websocket`, `github.com/gorilla/mux`, test với `go test`
- **Transport:** HTTP REST + WebSocket default `127.0.0.1:17890` (từ v2.0; trước đây `0.0.0.0:17890`); Bearer token `DOCKLITE_API_TOKEN` so sánh constant-time
- **Build:** `dotnet build DockLite.slnx` (.NET), `go build ./...` trong WSL; publish single-file qua `scripts/Publish-Wpf.ps1`
- **CI:** `.github/workflows/ci.yml` (build + test), `.github/workflows/release.yml` (SBOM CycloneDX + Cosign keyless signing)

Chi tiết đầy đủ: `.planning/codebase/STACK.md`.
<!-- gsd-stack-end -->

<!-- gsd-conventions-start source:codebase/CONVENTIONS.md -->
## Conventions

- **Ngôn ngữ giao tiếp:** Tất cả comment, doc, markdown, commit message, code message dùng **tiếng Việt**. Dùng `logger.LogError(...)` với message tiếng Việt cho user-facing log, English-only cho technical trace
- **Không icon/emoji** trong code, docs, markdown, commit
- **C#:** MVVM với `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`). DI qua constructor. Async suffix `Async`, chỉ cancel bằng `CancellationToken`. Không dùng `async void` trừ event handler. Naming: `PascalCase` cho public, `_camelCase` cho private field
- **Go:** Layout `cmd/` + `internal/`. Package phẳng theo domain (`internal/containers`, `internal/images`, `internal/compose`). Handler nhận `http.ResponseWriter, *http.Request`, return envelope qua `apiresponse.WriteOK` / `WriteError`. Không panic trong handler
- **API Envelope:** Mọi response JSON dạng `{ code, message, requestId, data }`. Lỗi map tới `DockLiteErrorCodes` (có định nghĩa song song ở C# `DockLite.Contracts.Api.DockLiteErrorCodes` và Go `internal/apiresponse`)
- **Logging:** C# dùng `ILogger<T>` (Serilog sink), Go dùng `slog` JSON. Không log token, không log fingerprint TLS đầy đủ
- **Test:** xUnit cho C# (naming `MethodName_Condition_Expected`), `go test` với table-driven cho Go. Mock HTTP bằng `HttpMessageHandler` custom

Chi tiết: `.planning/codebase/CONVENTIONS.md`.
<!-- gsd-conventions-end -->

<!-- gsd-architecture-start source:codebase/ARCHITECTURE.md -->
## Architecture

**Hai process tách biệt:**
1. **WPF (Windows):** `DockLite.App` (UI + ViewModel + App Services) → `DockLite.Core` (interface API client, cấu hình) → `DockLite.Infrastructure` (HTTP client, settings store, WSL lifecycle) → `DockLite.Contracts` (envelope, DTO)
2. **Go daemon (WSL):** `cmd/server` entrypoint → `internal/*` (containers, images, compose, wslimit, apiresponse, docker client wrapper) → Docker Engine API qua `github.com/docker/docker/client`

**Giao tiếp:** WPF gọi `IDockLiteApiClient` (REST) và `ILogStreamClient` / `IStatsStreamClient` (WebSocket) → gửi HTTP/WS tới `http://127.0.0.1:17890/*` → Go daemon gọi Docker Engine API → trả envelope JSON.

**Lifecycle:** `AppStartupCoordinator` (WPF) health-probe service; khi fail gọi `WslDockerServiceAutoStart.RestartAsync` → spawn `wsl.exe bash run-server.sh`. Service Go self-restart khi crash (giới hạn retry).

**Security zones (sau v2.0):**
- Trust zone 1: WPF process cùng user (đọc token từ Credential Manager, gọi loopback HTTP)
- Trust zone 2: Service Go trong WSL (loopback listen, token validate, rate-limit)
- Trust zone 3: Docker Engine (local socket, trust service Go đã auth)
- Trust zone 0 (hostile khi opt-in LAN): LAN network — buộc TLS + token + CheckOrigin + rate-limit

Chi tiết: `.planning/codebase/ARCHITECTURE.md`, `.planning/codebase/INTEGRATIONS.md`, `.planning/codebase/STRUCTURE.md`.
<!-- gsd-architecture-end -->

<!-- gsd-skills-start source:skills/ -->
## Project Skills

Repo này có tập skill GSD (Get Shit Done) tại `.cursor/skills/gsd-*/SKILL.md` (nếu cài GSD). Dùng slash command khi cần:

- `/gsd-progress` — xem trạng thái dự án
- `/gsd-plan-phase <N>` — plan phase chi tiết
- `/gsd-execute-phase` — thực thi phase theo wave parallel
- `/gsd-discuss-phase <N>` — thảo luận gray area trước khi plan
- `/gsd-next` — tự động chọn bước kế tiếp
- `/gsd-fast "<task>"` — sửa nhanh không overhead planning
- `/gsd-debug "<bug>"` — debug có state persistent

Skill tùy dự án: chưa có (bổ sung khi chạy wrap-up tương ứng nếu có).
<!-- gsd-skills-end -->

<!-- gsd-workflow-start source:GSD defaults -->
## GSD Workflow Enforcement

Trước khi dùng Edit, Write, hoặc công cụ sửa file, nên vào qua một GSD command để artifact planning và execution context đồng bộ.

Entry point:
- `/gsd-quick` cho fix nhỏ, update docs, ad-hoc task
- `/gsd-debug` cho investigation / bug fixing
- `/gsd-execute-phase` cho phase đã plan

Không sửa repo ngoài GSD workflow trừ khi user yêu cầu bypass rõ ràng.
<!-- gsd-workflow-end -->

<!-- gsd-security-start source:PROJECT.md#standards -->
## Security Standards (v2.0)

- **Chuẩn chính:** OWASP ASVS 4.0.3 L2 — mọi requirement v2.0 map tới control ID (`.planning/REQUIREMENTS.md`)
- **Chuẩn phụ:** NIST SP 800-207 Zero Trust — framing design
- **Verification:** Self-attestation trong `.planning/SECURITY-ATTESTATION.md` khi đóng v2.0

Khi review code hoặc viết test, mindset Zero-Trust: mọi input không trust, fail-closed mặc định, không log secret, dùng constant-time comparison cho token.

Tài liệu bối cảnh: `docs/docklite-lan-security.md` nếu có bản sao ngoài gitignore, `.planning/codebase/CONCERNS.md` (mục ưu tiên).
<!-- gsd-security-end -->

<!-- gsd-profile-start -->
## Developer Profile

> Profile chưa cấu hình. Chạy `/gsd-profile-user` để generate developer profile. Section này có thể do `generate-claude-profile` quản lý — hạn chế sửa tay nếu workflow đang dùng nó.
<!-- gsd-profile-end -->
