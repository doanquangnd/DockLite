---
gsd_state_version: 1.0
milestone: v2.1
milestone_name: Local-only Channel
status: planning_next_milestone
stopped_at: Đã archive v2.0; chờ /gsd-new-milestone để tạo REQUIREMENTS.md và roadmap chi tiết v2.1
last_updated: "2026-04-25T15:00:00.000Z"
last_activity: 2026-04-25 — /gsd-complete-milestone (archive + tag v2.0.0)
progress:
  percent: 0
---

# Project State

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-04-25)

**Core value:** DockLite chạy an toàn mặc định theo Zero-Trust; IPC WPF ↔ Go service là kênh tin cậy ngay cả khi opt-in mở LAN.
**Current focus:** v2.1 Local-only Channel — chạy `/gsd-new-milestone` để tạo `REQUIREMENTS.md` và phase; xem seed LCH trong `milestones/v2.0-REQUIREMENTS.md`

## Current Position

Phase: v2.0 đã ship và archive; v2.1 chưa bắt đầu
Plan: —
Status: Milestone v2.0 đóng (`MILESTONES.md`, `milestones/v2.0-*`, tag `v2.0.0`)
Last activity: 2026-04-25 — Hoàn tất `/gsd-complete-milestone`

Progress: 0% v2.1 (chờ khởi tạo milestone)

## Performance Metrics

**Velocity:**

- Total plans completed: 13
- Average duration: — min
- Total execution time: 0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 1. Network Surface Reduction | 2 | 2 | n/a |
| 2. Secrets at Rest | 3 | 3 | n/a |
| 3. Abuse Prevention and Audit | 2 | 2 | n/a |
| 4. TLS Opt-in TOFU | 3 | 3 | n/a |
| 5. Process Hardening | 3 | 3 | n/a |

**Recent Trend:** Phases 1–2 giao trong cùng milestone

## Accumulated Context

### Decisions

Full decision log trong `PROJECT.md` Key Decisions table. Recent decisions ảnh hưởng phase hiện tại:

- Milestone v2.0 gom 5 phase IPC hardening (không tách nhỏ)
- Dual channel là đích dài hạn; v2.0 chỉ TCP hardening, v2.1 thêm pipe
- Windows Credential Manager cho token storage (không DPAPI)
- Self-signed cert với TOFU fingerprint pinning (không mTLS)
- OWASP ASVS 4.0.3 L2 là chuẩn chính; NIST SP 800-207 là framing phụ
- Self-attestation (không pen test / CI automated security scan trong v2.0)
- Breaking change OK — major bump v1 → v2

### Pending Todos

Không có.

### Blockers/Concerns

Không có. Đã gỡ các blocker về scope trong Deep Questioning trước khi tạo PROJECT.md.

## Deferred Items

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| Local IPC Channel | Named pipe / Unix socket channel (LCH-01..04) | Deferred to v2.1 | 2026-04-23 (pre-init scoping) |
| CI Supply-chain | SLSA L3, automated security scan (govulncheck, ZAP, semgrep) | Deferred to milestone CI/CD riêng | 2026-04-23 |

## Session Continuity

Last session: 2026-04-25
Stopped at: Archive milestone v2.0, `git rm` REQUIREMENTS.md, tag v2.0.0.
Resume file: None
