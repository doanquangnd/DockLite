# Roadmap: DockLite

## Overview

DockLite đã ở v1.x với bộ tính năng Docker GUI đầy đủ (container, image, network, volume, compose, logs stream). Milestone v2.0 là milestone bảo mật: cứng hóa IPC giữa WPF và Go service theo nguyên tắc Zero-Trust. Toàn bộ 5 phase v2.0 map tới OWASP ASVS 4.0.3 L2 control và được self-attest qua `SECURITY-ATTESTATION.md` khi đóng milestone.

Sau v2.0 ship, roadmap sẽ có v2.1 bổ sung kênh IPC pipe/socket local-only (đã nằm trong REQUIREMENTS.md mục v2.1 deferred).

## Milestones

- SHIPPED — **v1.x Foundation** — Docker GUI với container, image, compose, logs, stats, auto-start WSL (đã released)
- IN-PROGRESS — **v2.0 IPC Hardening (Zero-Trust)** — Phases 1-5 (milestone hiện tại)
- PLANNED — **v2.1 Local-only Channel** — Pipe/Unix socket thay TCP mặc định

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (sẽ đánh dấu INSERTED)

- [ ] **Phase 1: Network Surface Reduction** — Bind loopback mặc định, fail-closed LAN mode, WebSocket CheckOrigin an toàn, UI warning cho non-loopback
- [ ] **Phase 2: Secrets at Rest** — Token chuyển từ settings.json plaintext sang Windows Credential Manager, migration tự động, endpoint rotate token
- [ ] **Phase 3: Abuse Prevention and Audit** — Rate-limit per-IP REST+WS, giảm timeout mặc định, audit log structured cho endpoint nhạy cảm, log rotating file
- [ ] **Phase 4: TLS Opt-in với TOFU** — Tự sinh self-signed cert ECDSA, trust-on-first-use fingerprint dialog, pin fingerprint vào Credential Manager, cert change warning
- [ ] **Phase 5: Process Hardening** — Vá shell injection `bash -lc`, validation `wslUnixPath`, PID file thay `pkill -f`, test injection argv cho trivy/compose, viết SECURITY-ATTESTATION.md

## Phase Details

### Phase 1: Network Surface Reduction

**Goal**: Giảm bề mặt tấn công network của service Go. Mặc định service không lắng nghe LAN; khi user opt-in LAN mode phải có token; WebSocket không accept origin ngoài loopback trừ khi whitelist; UI cảnh báo cấu hình rủi ro.

**Depends on**: Nothing (first phase của v2.0, độc lập với các phase sau)

**Requirements**: NET-01, NET-02, NET-03, NET-04, NET-05

**Success Criteria** (phải đúng sau khi phase đóng):
  1. Service Go chạy mặc định chỉ accept kết nối trên `127.0.0.1`; verify bằng `netstat -ano` chỉ thấy loopback listen
  2. Set `DOCKLITE_ADDR=0.0.0.0:17890` không token → service fail với exit code 2 và message lỗi rõ ràng trên stderr
  3. WebSocket upgrade từ origin `http://evil.com` bị từ chối với 403; origin `http://localhost` accept
  4. UI Settings hiển thị banner vàng khi BaseUrl cấu hình là `http://<ip-LAN>:17890`; banner đỏ khi non-HTTPS kèm non-loopback
  5. `.env.example` phản ánh mặc định loopback

**Plans**: 2 plans

Plans:
- [ ] 01-01: Service Go — bind default loopback, fail-closed LAN không token, WebSocket CheckOrigin whitelist (Go code + tests)
- [ ] 01-02: WPF — UI Settings banner cảnh báo non-loopback/non-HTTPS, cập nhật `.env.example` và docs (WPF + docs)

### Phase 2: Secrets at Rest

**Goal**: API token không còn sống dưới dạng plaintext trong file. Chuyển sang Windows Credential Manager với migration một chiều tự động. Cung cấp endpoint + UI để rotate token không phải sửa file tay.

**Depends on**: Phase 1 (dependency mềm — Phase 2 không cần Phase 1 đã ship, nhưng logic "fail-closed khi LAN + no token" của Phase 1 gắn với khả năng đọc/ghi token của Phase 2; chạy tuần tự rõ ràng hơn)

**Requirements**: SEC-01, SEC-02, SEC-03, SEC-04, SEC-05

**Success Criteria**:
  1. User đầu tiên chạy v2.0 trên máy cũ (có token v1 plaintext) thấy token tự động di chuyển sang Credential Manager; `settings.json` không còn field chứa token giá trị; app log ghi migration status
  2. Mở Windows Credential Manager UI (`control /name Microsoft.CredentialManager`), thấy entry `DockLite:ServiceApiToken:*` xuất hiện
  3. Endpoint `POST /api/auth/rotate` với token hiện tại trả token mới; token cũ sau đó không còn authenticate được
  4. Nút "Rotate API token" trong Settings WPF thực thi rotate và reload client HTTP với token mới; không cần restart app
  5. Cài bản clean (không có token cũ) và setup lần đầu — token sinh ra và lưu trực tiếp vào Credential Manager

**Plans**: 3 plans

Plans:
- [ ] 02-01: WPF — Windows Credential Manager wrapper (P/Invoke advapi32 hoặc PasswordVault), unit test read/write/delete
- [ ] 02-02: WPF — `AppSettingsStore` migration logic + loại field token khỏi JSON; `AppFileLog` ghi migration status
- [ ] 02-03: Go + WPF — Endpoint `POST /api/auth/rotate` atomic swap + WPF button rotate + reload HTTP session

### Phase 3: Abuse Prevention and Audit

**Goal**: Ngăn DoS và brute-force bằng rate-limit. Phát hiện hành vi bất thường qua audit log structured cho các endpoint phá hoại được (prune, pull, load, compose up/down, rotate).

**Depends on**: Phase 1 (middleware rate-limit gắn với router Go; CheckOrigin ở Phase 1 hoạt động trên cùng chain middleware)

**Requirements**: AUD-01, AUD-02, AUD-03, AUD-04

**Success Criteria**:
  1. Spam 100 req/s REST trong 5s từ một IP → 429 xuất hiện sau khi vượt burst; `Retry-After` header có mặt
  2. Mở 10 WebSocket upgrade liên tiếp trong 1s từ một IP → chỉ 2-4 đầu thành công, còn lại 429
  3. Gọi `POST /api/system/prune` với token hợp lệ → một dòng JSON audit log xuất hiện ở stdout VÀ file `~/.docklite/logs/audit-YYYY-MM-DD.log`
  4. Audit log record có đầy đủ fields (ts, remote_ip, user_agent, method, path, status, auth_status, request_id, latency_ms) và KHÔNG chứa token hay fingerprint
  5. HTTP `ReadTimeout` endpoint thường `<= 60s` (verify qua code hoặc slow-loris test); endpoint streaming (log/stats/images load) vẫn cho phép long-lived connection

**Plans**: 2 plans

Plans:
- [ ] 03-01: Go — rate-limit middleware per-IP + timeout differentiation per route group
- [ ] 03-02: Go — audit log middleware structured slog + rotating file sink

### Phase 4: TLS Opt-in với TOFU

**Goal**: Khi user mở LAN mode, họ có kênh HTTPS với cert tự sinh. Client WPF xác thực server qua trust-on-first-use fingerprint pinning, phát hiện được thay đổi cert (MITM).

**Depends on**: Phase 2 (cần Credential Manager để lưu fingerprint pin), Phase 1 (LAN mode là lý do để mở TLS)

**Requirements**: TLS-01, TLS-02, TLS-03, TLS-04, TLS-05, TLS-06

**Success Criteria**:
  1. Set `DOCKLITE_TLS_ENABLED=true` và start service → file `~/.docklite/tls/cert.pem` và `key.pem` xuất hiện với mode 0600; cert có CN="DockLite WSL Service", SAN chứa `127.0.0.1` và WSL IP
  2. WPF lần đầu connect HTTPS → dialog fingerprint SHA-256 xuất hiện; click Trust → kết nối thành công
  3. Xóa cert và regenerate (fingerprint mới), connect lại → dialog "Certificate changed" xuất hiện với fingerprint cũ và mới; Abort → kết nối thất bại
  4. Check Credential Manager → có entry `DockLite:TrustedFingerprint:*`
  5. Toggle "Enable TLS" trong Settings bật/tắt HTTPS runtime không crash app; nút "Forget pinned fingerprint" xóa pin và trigger TOFU dialog ở lần connect kế
  6. TLS session dùng TLS 1.2+ (verify qua `openssl s_client` hoặc `curl --tls-max 1.2 --tlsv1.2`)

**Plans**: 3 plans

Plans:
- [ ] 04-01: Go — tự sinh self-signed ECDSA cert + load/reuse + TLS server setup
- [ ] 04-02: WPF — HttpClient custom `ServerCertificateCustomValidationCallback` với fingerprint pin từ Credential Manager
- [ ] 04-03: WPF — UI dialog TOFU và cert change, Settings toggle TLS + forget fingerprint

### Phase 5: Process Hardening

**Goal**: Chặn mọi con đường injection vào lệnh shell/subprocess. Viết test regression cho các inject pattern đã biết. Đóng milestone bằng `SECURITY-ATTESTATION.md` self-attest toàn bộ 27 requirement.

**Depends on**: Phases 1, 2, 3, 4 (SECURITY-ATTESTATION tổng kết cần các phase trước đã có evidence; các fix injection độc lập và có thể chạy song song phase khác — nhưng self-attest cần cuối cùng)

**Requirements**: PRC-01, PRC-02, PRC-03, PRC-04, PRC-05, PRC-06, PRC-07

**Success Criteria**:
  1. Unit test `WslDockerServiceAutoStartTests` với inject `path = "/tmp/'; rm -rf /"` → test pass (reject hoặc escape đúng, không execute)
  2. Unit test `ImageTrivyScan` với `imageRef = "--format=json"` → reject; với `imageRef = "alpine:latest"` → execute đúng
  3. Unit test `composeServiceExec` với service name `-foo` → reject; service name `app` → execute đúng
  4. Stop app bằng cách kill WPF process → script stop trong WSL (nếu chạy bằng tay qua task scheduler hoặc manual) dùng PID file, không đụng process khác có tên chứa "docklite-wsl"
  5. File `.planning/SECURITY-ATTESTATION.md` tồn tại, map 27 requirement v2.0 tới status COVERED / PARTIAL / N/A với evidence file:line và test name
  6. Grep không tìm thấy `bash -lc` với user input interpolate trực tiếp trong codebase C# (`rg -n "bash -lc .*{"` trả zero hit trong file C# logic)

**Plans**: 3 plans

Plans:
- [ ] 05-01: WPF — sửa shell injection `WslDockerServiceAutoStart` + validation `wslUnixPath` + test injection
- [ ] 05-02: Go — PID file lifecycle script + argument injection guard cho trivy/compose + tests
- [ ] 05-03: Docs — viết `SECURITY-ATTESTATION.md` tự attest từng ASVS control, đính kèm evidence

## Progress

**Execution Order:**
Phases 1 → 2 → 3 → 4 → 5 (có thể chạy song song trong giới hạn dependency; parallelization enabled trong config).

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Network Surface Reduction | 0/2 | Not started | - |
| 2. Secrets at Rest | 0/3 | Not started | - |
| 3. Abuse Prevention and Audit | 0/2 | Not started | - |
| 4. TLS Opt-in với TOFU | 0/3 | Not started | - |
| 5. Process Hardening | 0/3 | Not started | - |

**Totals:** 5 phases, 13 plans.

---
*Roadmap defined: 2026-04-23 (v2.0 IPC Hardening milestone)*
