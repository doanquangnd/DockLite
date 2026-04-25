# Milestones DockLite (lịch sử ship)

## v2.0 — IPC Hardening (Zero-Trust)

**Ngày ship:** 2026-04-25  
**Phase:** 5 (1–5) | **Plan:** 13 | **Requirement v2.0:** 27 (đã attest)

### Đã giao (tóm tắt)

1. **Mạng:** Bind loopback mặc định; LAN fail-closed khi thiếu token; WebSocket CheckOrigin an toàn; cảnh báo UI cho URL rủi ro.
2. **Bí mật:** Token API trong Windows Credential Manager; migration từ plaintext; `POST /api/auth/rotate` và UI xoay token.
3. **Chống lạm dụng:** Rate-limit per-IP REST và WS upgrade; timeout chuẩn + deadline dài cho luồng dài; audit JSON (stdout + file xoay).
4. **TLS:** Cert tự ký ECDSA trên service; client WPF TOFU + pin fingerprint; cảnh báo đổi cert.
5. **Process:** Spawn WSL `--cd` + script trực tiếp; validate đường dẫn Unix; PID file cho lifecycle; `--` và test injection cho trivy/compose; `SECURITY-ATTESTATION.md`.

### Tài liệu archive

- [milestones/v2.0-ROADMAP.md](milestones/v2.0-ROADMAP.md)
- [milestones/v2.0-REQUIREMENTS.md](milestones/v2.0-REQUIREMENTS.md)

### Kiểm thử tối thiểu tại đóng

- `dotnet test` (DockLite.Tests)
- `go vet ./...` và `go test ./...` trong `wsl-docker-service`

### Ghi chú

- Không có file `v2.0-MILESTONE-AUDIT.md`; đóng milestone dựa trên attestation và audit-open (GSD) sạch.
- Requirement tiếp theo (v2.1 LCH) nằm trong archive requirements; tạo lại `REQUIREMENTS.md` bằng `/gsd-new-milestone`.

---

## v1.x Foundation

Ship trước milestone v2.0 (Docker GUI, compose, logs, stats, auto-start WSL, i18n, token tùy chọn, release SBOM). Không có file archive riêng trong repo tại thời điểm 2026-04-25.
