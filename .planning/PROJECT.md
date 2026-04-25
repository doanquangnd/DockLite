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

**v2.0 IPC Hardening (ship 2026-04-25):**

- Loopback mặc định; LAN fail-closed khi thiếu token; WebSocket CheckOrigin và cảnh báo UI — v2.0
- Token API và fingerprint TLS trong Credential Manager; migration plaintext; `POST /api/auth/rotate` và UI — v2.0
- Rate-limit per-IP; timeout và audit structured; file audit xoay — v2.0
- TLS tùy chọn trên service; TOFU và pin trên WPF — v2.0
- Spawn WSL `--cd` + bash script; validate đường dẫn Unix; PID file lifecycle; `--` và test injection trivy/compose; `SECURITY-ATTESTATION.md` — v2.0

### Active

<!-- v2.1 — Local-only Channel. Chi tiết sẽ được `/gsd-new-milestone` ghi vào REQUIREMENTS.md mới. Seed: milestones/v2.0-REQUIREMENTS.md (mục v2.1 Deferred). -->

- [ ] **LCH-01:** Service Go lắng nghe named pipe Windows hoặc Unix socket (interop WSL) tùy thiết kế đã chọn
- [ ] **LCH-02:** WPF chọn kênh pipe so với TCP qua cài đặt; pipe là mặc định mới khi sẵn sàng
- [ ] **LCH-03:** Chế độ pipe không phụ thuộc Bearer token (tin cậy qua ACL đúng user)
- [ ] **LCH-04:** Chế độ TCP giữ nguyên hành vi cứng hóa từ v2.0

### Out of Scope

<!-- Ranh giới dài hạn; cập nhật khi milestone mới làm rõ. -->

- **Chuyển kênh IPC sang Unix socket / Windows named pipe** — đã chuyển sang **v2.1** (Active ở trên); không làm trong v2.0.
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

**Hiện trạng (sau v2.0 ship, 2026-04-25):**

Milestone v2.0 đã khép các lỗ hổng ưu tiên (bind LAN mặc định, token plaintext, CheckOrigin, shell injection lifecycle, thiếu rate-limit/audit, thiếu TLS có kiểm soát). Bằng chứng và mapping ASVS nằm trong `.planning/SECURITY-ATTESTATION.md`. Lịch sử roadmap/requirement v2.0: `.planning/milestones/v2.0-ROADMAP.md`, `v2.0-REQUIREMENTS.md`.

**Tham chiếu chuẩn (không đổi):**

- OWASP ASVS 4.0.3 L2; NIST SP 800-207 Zero Trust (framing).

**Verification model:** Self-attestation đã duy trì cho v2.0; milestone tiếp theo có thể bổ sung kiểm thử tự động hoặc audit ngoài theo nhu cầu.

**Người dùng hiện tại:**

- Nhà phát triển .NET và Go trên WSL2; LAN opt-in vẫn cần ý thức rủi ro (tài liệu `docs/docklite-lan-security.md`).

**Breaking change (v1.x → v2.0) đã áp dụng:**

- Mặc định loopback; LAN cần token (và TLS khuyến nghị). Token đã chuyển sang Credential Manager khi upgrade.

## Constraints

- **Tech stack:** .NET 8 (WPF), C# 12, `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection`. Go 1.25+ (theo `wsl-docker-service/go.mod`), `github.com/docker/docker v26.1.5+incompatible`, `github.com/gorilla/websocket`.
- **Compatibility:** Chỉ hỗ trợ Windows 10 build 2004+ / Windows 11 với WSL2. Không nhắm Linux/macOS native.
- **Standards:** Requirement bảo mật đã ship (v2.0) map ASVS L2; milestone mới tiếp tục chuẩn này khi áp dụng.
- **Security:** Zero-Trust mindset; fail-closed mặc định cho mọi đường network. Không để log chứa token/fingerprint nguyên dạng.
- **Dependencies:** Hạn chế thêm dependency mới. Rate-limit dùng `golang.org/x/time/rate` (đã là indirect dep qua Docker client). Credential Manager dùng P/Invoke hoặc `System.Windows.Security` (framework built-in). Không thêm package NuGet thương mại.
- **Performance:** Thay đổi bảo mật không được làm giảm tốc độ stream log/stats quá 5% so với v1.x (benchmark trước khi release).
- **Backward compat:** Setting legacy token plaintext được migrate một lần, log rõ; sau migration settings.json mới không giữ token. User cài lại v2.0 trên máy từng dùng v1.x phải thấy UI ổn định (không mất config khác).
- **Timeline:** Không ràng buộc cứng. Self-hosted dev, ship khi xong.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Milestone v2.0 gom 5 phase IPC hardening thay vì tách nhỏ | Các phase có phụ thuộc logic (token migration ảnh hưởng tất cả), đóng gói như release bảo mật rõ ràng | Đã ship đúng cấu trúc 5 phase |
| Dual channel là kiến trúc đích dài hạn; v2.0 chỉ cứng hóa TCP | Phạm vi pipe/socket cần research transport WPF↔WSL2; TCP hardening mang lại 80% giá trị ngay | v2.0 hoàn tất TCP; v2.1 làm pipe |
| Windows Credential Manager cho token (không DPAPI) | Tách kho token khỏi settings.json giảm attack surface (backup tool, search indexer không quét vault); chuẩn hơn cho end-user Windows | Đã triển khai + migration |
| Self-signed cert với TOFU fingerprint pinning | Tránh phụ thuộc CA; user experience đơn giản; đủ cho threat model (LAN + MITM) | Đã triển khai Go + WPF |
| OWASP ASVS 4.0.3 L2 làm chuẩn chính, NIST SP 800-207 làm khung | ASVS cho checklist đo đếm được; NIST ZT cho framing; CIS Docker và Microsoft SDL quá chi tiết/quá nặng | Attestation v2.0 đã map |
| Breaking change OK — major bump v1→v2 | User opt-in LAN là nhóm nhỏ, có docs/docklite-lan-security.md đã cảnh báo; giữ backward compat đồng nghĩa giữ lỗ hổng | Đã ship; README/CHANGELOG ghi migrate |
| Self-attestation (không pen test / CI automated security scan trong v2.0) | Giảm scope; đưa automation sang milestone CI/CD riêng sau; pen test nội bộ qua unit test + manual checklist | SECURITY-ATTESTATION.md |
| Rate-limit middleware dùng `golang.org/x/time/rate` | Đã là indirect dep; không thêm package mới | Đã triển khai |
| Fail-closed khi non-loopback + token trống | Buộc user opt-in có ý thức khi mở LAN; giảm rủi ro default-exposure | Đã triển khai |
| Token rotation là endpoint server-side, không client-only | Không muốn WPF và service lệch token; rotation chain đảm bảo atomicity | POST /api/auth/rotate |
| Pipe/socket channel hoãn sang v2.1 (không cancel) | Vẫn là đích cuối; research cần thêm thời gian (vsock vs named pipe vs socat) | Chuyển sang Active v2.1 |
| mTLS và quyền phân cấp token không làm trong v2.0 | TOFU + single-role token đủ; thêm complexity không tương xứng giá trị ở giai đoạn này | Vẫn Out of Scope; reassess sau |

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

*Last updated: 2026-04-25 after đóng milestone v2.0 IPC Hardening (`/gsd-complete-milestone`)*
