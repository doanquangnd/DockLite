# DockLite

## What This Is

DockLite là ứng dụng desktop Windows (WPF + .NET 8) quản lý Docker chạy trong WSL qua một sidecar Go (`docklite-wsl`). WPF đóng vai thin client MVVM; mọi thao tác Docker đi qua HTTP REST + WebSocket tới sidecar Go, sidecar bao quanh Docker Engine API local socket. Sản phẩm dành cho nhà phát triển muốn thao tác container, image, network, volume, compose trên Windows mà không cần mở Docker Desktop.

## Core Value

DockLite phải chạy an toàn mặc định theo nguyên tắc Zero-Trust: không phơi Docker socket ra khỏi máy, API token không đọc được bởi process khác cùng user Windows, và khi người dùng cố ý mở kênh IPC ra LAN thì có TLS, rate-limit, và audit log bảo vệ. Nếu mọi tính năng khác thất bại, IPC giữa WPF và Go service vẫn phải là kênh tin cậy.

## Requirements

### Validated

<!-- Shipped và đã hoạt động trong v1.x. Suy ra từ `.planning/codebase/*`. -->

- Quản lý container (list, inspect, start/stop/restart/remove, stats realtime, logs stream) — v1.x
- Quản lý image (list, inspect, history, pull, load, remove, prune, export, quét Trivy tùy chọn) — v1.x
- Quản lý network và volume (list, remove) — v1.x
- Quản lý Docker Compose project (add, list, up, down, ps, logs stream, exec service) — v1.x
- Hiển thị Docker events stream (NDJSON) — v1.x
- System prune và Docker info/health — v1.x
- Tự khởi động / khởi động lại service Go trong WSL từ WPF qua `wsl.exe` — v1.x
- Đồng bộ source Go từ Windows sang WSL trước khi build (script hỗ trợ) — v1.x
- i18n (vi/en) và theme (light/dark) — v1.x
- Bearer token tùy chọn cho API (so sánh constant-time) — v1.x
- Release pipeline với SBOM CycloneDX và chữ ký Cosign keyless — v1.x

### Active

<!-- v2.0 — IPC Hardening (Zero-Trust). Các mục dưới đây là hypothesis cho tới khi ship và verify. -->

**Phase 1 — Network Surface Reduction:**
- [ ] Service Go mặc định bind `127.0.0.1:17890` (thay cho `0.0.0.0:17890`)
- [ ] Fail-closed: service từ chối khởi động nếu `DOCKLITE_ADDR` non-loopback VÀ `DOCKLITE_API_TOKEN` trống
- [ ] WebSocket `CheckOrigin` mặc định chỉ cho phép `localhost`, `127.0.0.1`, `[::1]`; whitelist mở rộng qua env `DOCKLITE_ALLOWED_ORIGINS`
- [ ] UI Settings hiển thị cảnh báo nổi bật khi `ServiceBaseUrl` không phải loopback hoặc scheme không phải HTTPS
- [ ] Cập nhật `.env.example` khuyến nghị loopback mặc định

**Phase 2 — Secrets at Rest:**
- [ ] Di chuyển `ServiceApiToken` từ `settings.json` plaintext sang Windows Credential Manager (`CredRead`/`CredWrite` qua `Windows.Security.Credentials.PasswordVault` hoặc P/Invoke `advapi32.dll`)
- [ ] `settings.json` chỉ giữ tham chiếu tên credential, không giữ token
- [ ] Migration một chiều: lần đầu chạy v2.0, đọc token plaintext cũ, ghi sang CredMan, xóa khỏi settings.json (có log)
- [ ] Endpoint `POST /api/auth/rotate` xoay token (nhận token cũ, trả token mới; WPF lưu tự động)
- [ ] UI Settings có nút "Rotate API token" và hiển thị trạng thái credential storage

**Phase 3 — Abuse Prevention:**
- [ ] Middleware rate-limit per-IP: 30 req/s REST, 2 upgrade/s WebSocket (`golang.org/x/time/rate`)
- [ ] Giảm `ReadTimeout`/`WriteTimeout` xuống 60s khi auth tắt HOẶC addr = loopback (vẫn giữ 30 phút cho `/api/images/load`, `/api/compose/*`)
- [ ] Audit log cấu trúc cho endpoint nhạy cảm (`/api/system/prune`, `/api/compose/up|down`, `/api/images/load|pull|remove|prune`, `/api/auth/rotate`): remote IP, User-Agent, endpoint, method, HTTP status, auth status, request ID
- [ ] Audit log ghi `slog` JSON vào stdout kèm file rotating tại `~/.docklite/logs/audit-*.log`

**Phase 4 — TLS Opt-in:**
- [ ] Service Go tự sinh self-signed cert (RSA 2048 hoặc ECDSA P-256) lần đầu khi `DOCKLITE_TLS_ENABLED=true`; lưu tại `~/.docklite/tls/cert.pem`, `key.pem` (chmod 0600)
- [ ] Cert metadata: CN = "DockLite WSL Service", SAN = `127.0.0.1`, WSL IP, hostname; hiệu lực 10 năm
- [ ] WPF thực hiện Trust-On-First-Use: lần đầu kết nối TLS, hiển thị dialog fingerprint SHA-256, cho phép "Trust" (pin) hoặc "Reject"
- [ ] Fingerprint pinned lưu trong Credential Manager (cùng vault với token)
- [ ] Khi fingerprint đổi ở lần connect sau, WPF hiển thị dialog "Certificate changed — do you trust?"
- [ ] UI Settings có toggle "Enable TLS" và hiển thị fingerprint hiện tại

**Phase 5 — Process Hardening:**
- [ ] Fix shell injection trong `WslDockerServiceAutoStart.SpawnWslLifecycleScript` bằng `BashSingleQuoted` (đã tồn tại trong file) HOẶC chuyển sang `wsl.exe -d <distro> --cd <unixPath> -- bash <script>` (bỏ `bash -lc`)
- [ ] Validation nghiêm ngặt `wslUnixPath`: reject ký tự `'`, `"`, backtick, `$`, `\n`, `\r`, `;`, `|`, `&`
- [ ] PID file cho `run-server.sh`, `stop-server.sh`, `restart-server.sh` thay cho `pkill -f`
- [ ] Unit test inject path độc hại cho `SpawnWslLifecycleScript`
- [ ] Unit test argument injection cho `ImageTrivyScan` (leading `-` trong `imageRef`) và `composeServiceExec` (leading `-` trong service name, profile)
- [ ] Thêm `--` separator trước user input khi gọi `docker compose` và `trivy`
- [ ] `SECURITY-ATTESTATION.md` tổng hợp self-attestation ASVS L2 checklist cho toàn v2.0

### Out of Scope

<!-- Ranh giới cứng cho v2.0. Kèm lý do để tránh re-add. -->

- **Chuyển kênh IPC sang Unix socket / Windows named pipe** — scope quá lớn cho v2.0, cần research về transport Windows ↔ WSL2 (vsock, wsl-interop pipe, socat proxy). Chuyển sang **v2.1 "Local-only channel"**.
- **mTLS client certificate** — phức tạp cho end-user (phải phát, quản, rotate cert client); TOFU fingerprint đã đủ cho threat model đã chọn. Reassess khi có dev team dùng DockLite shared.
- **Mã hóa toàn bộ `settings.json`** — chỉ token là sensitive; các field UI/path/timeout không cần mã hóa. Nếu mã hóa toàn bộ, phải derive key từ đâu → kéo theo master password UX.
- **Quyền phân cấp read-only vs admin token** — v2.0 giữ model token single-role. Reassess sau khi có audit log để biết usage pattern thực.
- **Gửi log sang SIEM bên ngoài (Splunk, ELK, OpenTelemetry OTLP)** — DockLite là local tool cho cá nhân dev; audit log file đủ cho forensic cá nhân. Reassess nếu có enterprise deployment.
- **SLSA level 3 / supply chain hardening CI/CD** — chuyển sang milestone riêng "CI/CD Hardening" (v2.2 hoặc v3.0). v2.0 không động vào `.github/workflows/`.
- **Tự động chạy `govulncheck`, ZAP, nuclei, semgrep trong CI** — self-attest thủ công cho v2.0 là đủ. Đưa vào milestone CI/CD Hardening sau.
- **CIS Docker Benchmark mapping chính thức** — chọn ASVS L2 làm chuẩn chính; CIS Docker chỉ tham chiếu gián tiếp qua codebase docs khi liên quan.
- **Microsoft SDL full process** — quá nặng cho milestone này; áp dụng rải rác nhưng không commit full threat modeling / fuzzing.
- **CWE Top 25 tagging** — overhead không cần khi đã map ASVS; tag gián tiếp qua self-attestation report nếu cần.

## Context

**Hiện trạng (v1.x, đánh giá 2026-04-23):**

Bản đồ codebase đã được ánh xạ trong `.planning/codebase/` (7 tài liệu, 1708 dòng). Các phát hiện chính đã có trong `.planning/codebase/CONCERNS.md` (phần "Checklist khắc phục ưu tiên" liệt kê 10 mục) và `docs/docklite-lan-security.md`. Tóm tắt các lỗ hổng trọng yếu thúc đẩy milestone này:

- **Service Go mặc định bind `0.0.0.0:17890`** (`wsl-docker-service/cmd/server/main.go:15-18`): mọi máy trong LAN truy cập được Docker API nếu token trống → root-on-host.
- **Token API plaintext trong `%LocalAppData%\DockLite\settings.json`** (`src/DockLite.Infrastructure/Configuration/AppSettingsStore.cs:18-23`): mọi process cùng user Windows đọc được.
- **WebSocket `CheckOrigin: true`** (`wsl-docker-service/internal/wslimit/wslimit.go:32`): CSWSH khi không có token hoặc client non-browser.
- **Shell injection tiềm ẩn trong `WslDockerServiceAutoStart.SpawnWslLifecycleScript`** (`src/DockLite.Infrastructure/Wsl/WslDockerServiceAutoStart.cs:905`): `wslUnixPath` nội suy vào `bash -lc` không escape.
- **Không rate-limit REST/WS**: slow-loris DoS trên socket timeout 30 phút.
- **HTTP không TLS** khi user opt-in LAN mode — token lộ qua sniff.
- **Không audit log** cho endpoint nhạy cảm.

**Tham chiếu chuẩn:**

- **Chuẩn chính:** OWASP ASVS 4.0.3 L2 (mapping từng requirement tới control ID). Chapter trọng tâm:
  - V1 Architecture, Design and Threat Modeling
  - V2 Authentication (token, rotation)
  - V9 Communications (TLS, CheckOrigin)
  - V10 Malicious Code (shell injection, argument injection)
  - V13 API and Web Service (rate-limit, audit)
  - V14 Configuration (default fail-closed, secret storage)
- **Chuẩn phụ:** NIST SP 800-207 Zero Trust — framework naming (PEP = service Go middleware; PDP = token validation + rate-limit; trust zones = loopback vs LAN vs internet).

**Verification model:** Self-attestation. Sau khi v2.0 đóng, viết `SECURITY-ATTESTATION.md` liệt kê từng ASVS L2 control với trạng thái COVERED / PARTIAL / N/A và evidence (file + line range + test).

**Người dùng hiện tại:**

- Nhà phát triển .NET và Go sử dụng Docker qua WSL2 trên Windows 10/11
- Sử dụng DockLite local một mình là chủ yếu; một số dùng ở chế độ LAN cho dev team nhỏ (docs/docklite-lan-security.md đã cảnh báo rủi ro)

**Breaking change strategy:**

v1.x → v2.0 là major bump. User opt-in LAN sẽ phải set `DOCKLITE_ADDR=0.0.0.0:17890` (thay vì mặc định như trước) VÀ bắt buộc set `DOCKLITE_API_TOKEN`. Release note ghi rõ lệnh shell để migrate. Migration token plaintext → Credential Manager là một chiều, tự động, có log.

## Constraints

- **Tech stack:** .NET 8 (WPF), C# 12, `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection`. Go 1.22+ (theo `wsl-docker-service/go.mod`), `github.com/docker/docker v26.1.5+incompatible`, `github.com/gorilla/websocket`. Không đổi stack trong v2.0.
- **Compatibility:** Chỉ hỗ trợ Windows 10 build 2004+ / Windows 11 với WSL2. Không nhắm Linux/macOS native.
- **Standards:** Mọi requirement v2.0 map tới OWASP ASVS 4.0.3 L2 control ID. Tài liệu NIST SP 800-207 dùng cho design framing.
- **Security:** Zero-Trust mindset; fail-closed mặc định cho mọi đường network. Không để log chứa token/fingerprint nguyên dạng.
- **Dependencies:** Hạn chế thêm dependency mới. Rate-limit dùng `golang.org/x/time/rate` (đã là indirect dep qua Docker client). Credential Manager dùng P/Invoke hoặc `System.Windows.Security` (framework built-in). Không thêm package NuGet thương mại.
- **Performance:** Thay đổi bảo mật không được làm giảm tốc độ stream log/stats quá 5% so với v1.x (benchmark trước khi release).
- **Backward compat:** Setting legacy token plaintext được migrate một lần, log rõ; sau migration settings.json mới không giữ token. User cài lại v2.0 trên máy từng dùng v1.x phải thấy UI ổn định (không mất config khác).
- **Timeline:** Không ràng buộc cứng. Self-hosted dev, ship khi xong.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Milestone v2.0 gom 5 phase IPC hardening thay vì tách nhỏ | Các phase có phụ thuộc logic (token migration ảnh hưởng tất cả), đóng gói như release bảo mật rõ ràng | — Pending |
| Dual channel là kiến trúc đích dài hạn; v2.0 chỉ cứng hóa TCP | Phạm vi pipe/socket cần research transport WPF↔WSL2; TCP hardening mang lại 80% giá trị ngay | — Pending |
| Windows Credential Manager cho token (không DPAPI) | Tách kho token khỏi settings.json giảm attack surface (backup tool, search indexer không quét vault); chuẩn hơn cho end-user Windows | — Pending |
| Self-signed cert với TOFU fingerprint pinning | Tránh phụ thuộc CA; user experience đơn giản; đủ cho threat model (LAN + MITM) | — Pending |
| OWASP ASVS 4.0.3 L2 làm chuẩn chính, NIST SP 800-207 làm khung | ASVS cho checklist đo đếm được; NIST ZT cho framing; CIS Docker và Microsoft SDL quá chi tiết/quá nặng | — Pending |
| Breaking change OK — major bump v1→v2 | User opt-in LAN là nhóm nhỏ, có docs/docklite-lan-security.md đã cảnh báo; giữ backward compat đồng nghĩa giữ lỗ hổng | — Pending |
| Self-attestation (không pen test / CI automated security scan trong v2.0) | Giảm scope; đưa automation sang milestone CI/CD riêng sau; pen test nội bộ qua unit test + manual checklist | — Pending |
| Rate-limit middleware dùng `golang.org/x/time/rate` | Đã là indirect dep; không thêm package mới | — Pending |
| Fail-closed khi non-loopback + token trống | Buộc user opt-in có ý thức khi mở LAN; giảm rủi ro default-exposure | — Pending |
| Token rotation là endpoint server-side, không client-only | Không muốn WPF và service lệch token; rotation chain đảm bảo atomicity | — Pending |
| Pipe/socket channel hoãn sang v2.1 (không cancel) | Vẫn là đích cuối; research cần thêm thời gian (vsock vs named pipe vs socat) | — Pending |
| mTLS và quyền phân cấp token không làm trong v2.0 | TOFU + single-role token đủ; thêm complexity không tương xứng giá trị ở giai đoạn này | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference (e.g. "v2.0 Phase 1")
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state (users, feedback, metrics)

---

*Last updated: 2026-04-23 after initialization (v2.0 IPC Hardening milestone scoping)*
