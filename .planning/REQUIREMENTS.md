# Requirements: DockLite

**Defined:** 2026-04-23
**Core Value:** DockLite phải chạy an toàn mặc định theo Zero-Trust; IPC giữa WPF và Go service là kênh tin cậy ngay cả khi user opt-in mở LAN.

Mỗi requirement v2.0 có **ASVS Control** map tới OWASP ASVS 4.0.3 L2. `SECURITY-ATTESTATION.md` cuối v2.0 sẽ self-attest COVERED / PARTIAL / N/A cho từng mục.

## v2.0 Requirements — IPC Hardening (Zero-Trust)

### Network Surface Reduction (NET)

- [ ] **NET-01**: Service Go mặc định bind `127.0.0.1:17890` thay cho `0.0.0.0:17890`. (ASVS V14.1.4, V1.14.3)
- [ ] **NET-02**: Service Go fail-closed — từ chối khởi động nếu `DOCKLITE_ADDR` non-loopback VÀ `DOCKLITE_API_TOKEN` trống. Log lý do vào stderr trước khi exit code 2. (ASVS V14.2.1, V14.1.1)
- [ ] **NET-03**: WebSocket `CheckOrigin` mặc định chỉ accept `localhost`, `127.0.0.1`, `[::1]`. Env `DOCKLITE_ALLOWED_ORIGINS` mở rộng whitelist khi user opt-in LAN mode. (ASVS V13.2.6, V9.3.1)
- [ ] **NET-04**: UI Settings hiển thị warning banner khi `ServiceBaseUrl` non-loopback hoặc scheme không phải HTTPS. Text warning ở light/dark đều đạt contrast AA. (ASVS V14.4.1)
- [ ] **NET-05**: `.env.example` và docs cập nhật: mặc định loopback, nêu rõ LAN mode là opt-in và cần token + TLS. (ASVS V14.1.1)

### Secrets at Rest (SEC)

- [ ] **SEC-01**: `ServiceApiToken` lưu trong Windows Credential Manager (`CredWrite`/`CredRead` qua P/Invoke `advapi32.dll` hoặc `PasswordVault`). Target name theo pattern `DockLite:ServiceApiToken:{profile}`. (ASVS V2.10.4, V6.1.1, V6.1.2)
- [ ] **SEC-02**: `settings.json` chỉ giữ target name credential, KHÔNG giữ token plaintext. Field `ServiceApiToken` trong model legacy được loại bỏ khỏi serialize path. (ASVS V2.10.1, V6.2.1)
- [ ] **SEC-03**: Migration một chiều tự động lần đầu v2.0: đọc token cũ từ `settings.json`, ghi sang Credential Manager, ghi đè settings.json (token cũ bị xóa hoàn toàn khỏi file). Migration log ghi vào `AppFileLog` với trạng thái (migrated / already-migrated / no-legacy-token). (ASVS V2.10.4)
- [ ] **SEC-04**: Endpoint `POST /api/auth/rotate` xoay token: body nhận `{ "current_token": "..." }`, trả về `{ "new_token": "..." }`. Service Go atomic swap token trong memory, invalidate cũ ngay lập tức. Rate-limit 5 req/phút/IP. (ASVS V2.5.3, V3.3.1, V13.1.5)
- [ ] **SEC-05**: UI Settings có nút "Rotate API token" và indicator trạng thái credential storage (ví dụ "Stored in Windows Credential Manager"). (ASVS V2.10.3)

### Abuse Prevention & Audit (AUD)

- [ ] **AUD-01**: Middleware rate-limit per-IP dùng `golang.org/x/time/rate`: 30 req/s REST (burst 60), 2 upgrade/s WebSocket (burst 4). Khi vượt trả 429 với `Retry-After`. (ASVS V11.1.4, V13.2.5)
- [ ] **AUD-02**: HTTP server `ReadTimeout`/`WriteTimeout` giảm xuống 60s mặc định, giữ 30 phút CHỈ cho whitelist endpoint streaming: `/api/images/load`, `/api/images/pull`, `/api/compose/up`, `/api/compose/down`, `/api/containers/{id}/logs/stream`, `/api/containers/{id}/stats/stream`, `/api/compose/services/logs`. (ASVS V11.1.1, V11.1.4)
- [ ] **AUD-03**: Audit log `slog` JSON structured cho endpoint nhạy cảm (`/api/system/prune`, `/api/compose/up`, `/api/compose/down`, `/api/images/load`, `/api/images/pull`, `/api/images/remove`, `/api/images/prune`, `/api/auth/rotate`). Fields: `ts`, `remote_ip`, `user_agent`, `method`, `path`, `status`, `auth_status` (none|valid|invalid), `request_id`, `latency_ms`. Không được log token, không log fingerprint nguyên. (ASVS V7.1.1, V7.1.3, V8.3.4)
- [ ] **AUD-04**: Audit log ghi stdout VÀ file rotating `~/.docklite/logs/audit-YYYY-MM-DD.log`, giữ 14 ngày, rotate size 10 MB. (ASVS V7.1.4, V7.2.1)

### TLS Opt-in với TOFU (TLS)

- [ ] **TLS-01**: Khi `DOCKLITE_TLS_ENABLED=true`, service Go tự sinh self-signed cert ECDSA P-256 lần đầu; lưu `~/.docklite/tls/cert.pem` và `key.pem` với `chmod 0600`. Nếu file tồn tại, reuse. (ASVS V9.1.1, V9.2.1, V6.2.6)
- [ ] **TLS-02**: Cert tự sinh có `CN = "DockLite WSL Service"`, `SAN = 127.0.0.1, ::1, <WSL IP>, <hostname>`, hiệu lực 10 năm, key usage `digitalSignature,keyEncipherment`, EKU `serverAuth`. (ASVS V9.2.2, V9.2.3)
- [ ] **TLS-03**: WPF trust-on-first-use — lần đầu connect TLS tới một fingerprint chưa pin, hiển thị dialog với fingerprint SHA-256 (format `XX:XX:...`), subject, validity dates; user chọn "Trust" hoặc "Reject". Reject đóng connection, không lưu. (ASVS V9.2.4)
- [ ] **TLS-04**: Fingerprint pin lưu trong Credential Manager cùng vault với token (target `DockLite:TrustedFingerprint:{host}:{port}`). (ASVS V2.10.4, V9.2.4)
- [ ] **TLS-05**: Khi fingerprint đổi ở lần connect sau, WPF hiển thị dialog "Certificate changed — possibly MITM" với fingerprint cũ và mới; user chọn "Trust new" hoặc "Abort". Abort không kết nối. (ASVS V9.2.4, V14.4.5)
- [ ] **TLS-06**: UI Settings có toggle "Enable TLS" và ô hiển thị fingerprint đang trust cho host hiện tại; nút "Forget pinned fingerprint" để reset trust. (ASVS V9.1.1)

### Process Hardening & Input Sanitization (PRC)

- [ ] **PRC-01**: `WslDockerServiceAutoStart.SpawnWslLifecycleScript` không còn dùng `bash -lc "cd '...' && ./script"`. Chuyển sang `wsl.exe -d <distro> --cd <unixPath> -- bash <script>` (native argv, không có shell quoting) HOẶC giữ `bash -lc` nhưng quote path qua helper `BashSingleQuoted` và reject path chứa single-quote. (ASVS V5.3.8, V10.2.3)
- [ ] **PRC-02**: Validation `wslUnixPath` reject các ký tự: `'`, `"`, backtick `` ` ``, `$`, `\n`, `\r`, `;`, `|`, `&`, `<`, `>`. Violation throw `ArgumentException` với message nêu ký tự vi phạm. (ASVS V5.1.5, V5.2.5)
- [ ] **PRC-03**: Lifecycle script `run-server.sh`, `stop-server.sh`, `restart-server.sh` dùng PID file (`~/.docklite/run/docklite-wsl.pid`) thay cho `pkill -f docklite-wsl`. Stop script đọc PID, `kill -TERM $pid`; timeout 5s rồi `kill -KILL`. (Operational safety — không có ASVS cụ thể, thuộc V14.2.5 hardening process)
- [ ] **PRC-04**: Unit test `WslDockerServiceAutoStartTests` thêm case inject: path chứa `'`, path chứa `$(rm -rf)`, path có newline. Expected: throw hoặc escaped đúng, KHÔNG execute injected command. (ASVS V5.3.8, Testing)
- [ ] **PRC-05**: Unit test argument injection cho `ImageTrivyScan` (leading `-` trong `imageRef` phải reject) và `composeServiceExec` (service name bắt đầu bằng `-` phải reject, profile name chứa `;` phải reject). Mỗi handler nhận một suite test ít nhất 3 malicious input. (ASVS V5.3.8, V12.3.4)
- [ ] **PRC-06**: Mọi lệnh spawn `docker compose`, `trivy`, `docker` CLI (trong Go và WPF) bắt buộc có `--` separator trước user-controlled positional arg. (ASVS V5.3.8)
- [ ] **PRC-07**: `SECURITY-ATTESTATION.md` cuối milestone liệt kê toàn bộ requirement v2.0 với self-attest COVERED / PARTIAL / N/A + evidence (file + line range + test name). (ASVS V1.1.2, V1.1.6)

## v2.1 Requirements (Deferred — Pipe Channel)

Đã acknowledge nhưng không có trong roadmap v2.0. Khi kích hoạt v2.1, chuyển sang Active.

### Local Channel (LCH)

- **LCH-01**: Service Go hỗ trợ lắng nghe trên Windows named pipe `\\.\pipe\docklite-wsl` hoặc Unix socket chuyển tiếp qua WSL interop
- **LCH-02**: WPF client chọn kênh (pipe vs TCP) qua setting; pipe là mặc định mới
- **LCH-03**: Pipe mode không cần token (trust qua ACL pipe đúng user Windows)
- **LCH-04**: TCP mode giữ nguyên cứng hóa từ v2.0

## Out of Scope

Những feature đã xem xét và loại rõ. Ghi lại để chống scope creep.

| Feature | Lý do |
|---------|-------|
| mTLS client certificate | TOFU fingerprint đã đủ cho threat model; mTLS phức tạp cho end-user dev (phát/rotate cert client). Reassess khi có team dùng DockLite shared |
| Mã hóa toàn bộ `settings.json` | Chỉ token là sensitive; mã hóa toàn bộ đẻ ra UX master password không cần thiết |
| Quyền phân cấp read-only / admin token | Giữ single-role token trong v2.0; reassess sau khi có audit log thực tế |
| Gửi log sang SIEM / OpenTelemetry | DockLite là local dev tool; file log đủ cho forensic cá nhân |
| SLSA L3 / CI supply-chain hardening | Tách milestone riêng "CI/CD Hardening" sau; v2.0 không động `.github/workflows/` |
| Tự động ZAP / govulncheck / semgrep trong CI | Self-attest thủ công đủ cho v2.0; automation sang milestone sau |
| CIS Docker Benchmark mapping chính thức | Chọn ASVS L2 làm chuẩn chính để gọn; CIS Docker chỉ tham chiếu gián tiếp trong docs |
| CWE Top 25 tagging từng finding | Overhead không cần khi đã map ASVS |
| Microsoft SDL full process (threat modeling, fuzz) | Quá nặng cho 1 milestone; áp dụng rải rác nhưng không commit full process |
| Pipe / named-pipe channel trong v2.0 | Cần research thêm transport WPF↔WSL2 (vsock vs named pipe vs socat). Chuyển sang v2.1 |
| Đổi HTTP framework (ví dụ sang `chi`, `echo`, `fiber`) | Stack `net/http` + `gorilla/mux` đã ổn; đổi framework là yak shave |
| Rewrite WPF sang WinUI 3 | Không liên quan bảo mật; dự án riêng, không trong v2.0 |

## Traceability

Mapping requirement ↔ phase. Cập nhật khi roadmap đổi.

| Requirement | Phase | Status |
|-------------|-------|--------|
| NET-01 | Phase 1 | Pending |
| NET-02 | Phase 1 | Pending |
| NET-03 | Phase 1 | Pending |
| NET-04 | Phase 1 | Pending |
| NET-05 | Phase 1 | Pending |
| SEC-01 | Phase 2 | Pending |
| SEC-02 | Phase 2 | Pending |
| SEC-03 | Phase 2 | Pending |
| SEC-04 | Phase 2 | Pending |
| SEC-05 | Phase 2 | Pending |
| AUD-01 | Phase 3 | Pending |
| AUD-02 | Phase 3 | Pending |
| AUD-03 | Phase 3 | Pending |
| AUD-04 | Phase 3 | Pending |
| TLS-01 | Phase 4 | Pending |
| TLS-02 | Phase 4 | Pending |
| TLS-03 | Phase 4 | Pending |
| TLS-04 | Phase 4 | Pending |
| TLS-05 | Phase 4 | Pending |
| TLS-06 | Phase 4 | Pending |
| PRC-01 | Phase 5 | Pending |
| PRC-02 | Phase 5 | Pending |
| PRC-03 | Phase 5 | Pending |
| PRC-04 | Phase 5 | Pending |
| PRC-05 | Phase 5 | Pending |
| PRC-06 | Phase 5 | Pending |
| PRC-07 | Phase 5 | Pending |

**Coverage:**
- v2.0 requirements: 27 total
- Mapped to phases: 27
- Unmapped: 0

---
*Requirements defined: 2026-04-23*
*Last updated: 2026-04-23 after initialization (v2.0 IPC Hardening milestone)*
