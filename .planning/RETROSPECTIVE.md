# Retrospective DockLite

Tài liệu sống, cập nhật sau mỗi milestone.

## Milestone: v2.0 — IPC Hardening (Zero-Trust)

**Ship:** 2026-04-25  
**Phase:** 5 | **Plan:** 13

### Đã xây dựng

- Giảm bề mặt mạng mặc định và fail-closed khi mở LAN không token.
- Token và fingerprint TLS trong Credential Manager; xoay token server-side.
- Rate-limit, timeout hợp lý, audit có cấu trúc không lộ secret.
- HTTPS tùy chọn trên service Go với TOFU trên WPF.
- Loại bỏ shell injection lifecycle WSL; PID file; kiểm thử injection cho CLI và compose.

### Điều hiệu quả

- Chia 5 phase theo ASVS giúp trace và attestation rõ ràng.
- Song song Go và WPF sau khi interface (token store, TLS) ổn định.

### Chưa tối ưu

- Bảng traceability trong REQUIREMENTS ban đầu không đồng bộ checkbox (đã sửa trong archive).
- Một số bước kiểm thử Go phụ thuộc môi trường có `go` trên máy build.

### Quy ước giữ lại

- Self-attest bằng `SECURITY-ATTESTATION.md` + evidence file:line cho mỗi requirement bảo mật.
- Không log token hay fingerprint đầy đủ; comment và doc tiếng Việt cho artifact nội bộ.

### Bài học

1. Đồng bộ bảng traceability với checkbox ngay khi đóng phase cuối.
2. Spawn process: ưu tiên argv tách (`wsl --cd`) thay chuỗi shell ghép user input.

### Chi phí / công cụ

- Không thu thập model mix tự động; milestone chủ yếu implementation + test cục bộ.

---

## Xu hướng xuyên milestone

| Milestone | Chủ đề bảo mật chính |
|-----------|----------------------|
| v1.x | Tính năng GUI + token tùy chọn |
| v2.0 | Zero-Trust IPC (TCP) |
| v2.1 (kế hoạch) | Kênh local-only (pipe/socket) |

Cập nhật bảng này khi đóng các milestone sau.
