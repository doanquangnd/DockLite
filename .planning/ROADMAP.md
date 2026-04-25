# Roadmap: DockLite

## Overview

v2.0 **IPC Hardening (Zero-Trust)** đã ship (2026-04-25): loopback mặc định, token Credential Manager, rate-limit và audit, TLS tùy chọn với TOFU, process hardening và `SECURITY-ATTESTATION.md`. Chi tiết phase và plan: [milestones/v2.0-ROADMAP.md](milestones/v2.0-ROADMAP.md).

Milestone kế tiếp **v2.1 Local-only Channel**: named pipe / Unix socket để giảm phụ thuộc TCP; seed requirement trong [milestones/v2.0-REQUIREMENTS.md](milestones/v2.0-REQUIREMENTS.md) (mục v2.1 Deferred).

## Milestones

- SHIPPED — **v1.x Foundation** — Docker GUI đầy đủ qua WPF + sidecar Go
- SHIPPED — **v2.0 IPC Hardening** — 2026-04-25 — [archive roadmap](milestones/v2.0-ROADMAP.md) · [archive requirements](milestones/v2.0-REQUIREMENTS.md)
- PLANNED — **v2.1 Local-only Channel** — Pipe/socket; chạy `/gsd-new-milestone` để tạo `REQUIREMENTS.md` và chia phase

## Phases

<details>
<summary>v2.0 IPC Hardening (Phases 1–5) — SHIPPED 2026-04-25</summary>

- [x] Phase 1: Network Surface Reduction — 2026-04-23
- [x] Phase 2: Secrets at Rest — 2026-04-23
- [x] Phase 3: Abuse Prevention and Audit — 2026-04-23
- [x] Phase 4: TLS Opt-in với TOFU — 2026-04-25
- [x] Phase 5: Process Hardening — 2026-04-25

Tổng: 5 phase, 13 plan (xem archive).

</details>

### v2.1 Local-only Channel (kế hoạch)

- [ ] Định nghĩa phase và plan sau khi có `REQUIREMENTS.md` mới (LCH-01..04 trong archive v2.0)
- [ ] Research transport WPF ↔ WSL2 (named pipe, unix socket, vsock) và mặc định kênh mới

## Progress (v2.1)

| Hạng mục | Trạng thái | Ghi chú |
|----------|------------|---------|
| Requirements v2.1 | Chưa tạo file | `/gsd-new-milestone` |
| Phase / plan | Chưa bắt đầu | — |

---
*Roadmap cập nhật: 2026-04-25 sau đóng milestone v2.0*
