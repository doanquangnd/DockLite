# Phase 3 — Tóm tắt thực thi

**Ngày:** 2026-04-23  
**Kế hoạch:** 03-01, 03-02

## Đã giao

### 03-01 — Rate limit + timeout (AUD-01, AUD-02)

- `internal/httpserver/clientip.go`: IP từ `X-Forwarded-For` (phần đầu) hoặc `RemoteAddr` (bỏ cổng).
- `PerIPRateLimit`: REST 30/s, burst 60; WebSocket upgrade (header `Upgrade: websocket`) 2/s, burst 4. Trả 429 + `Retry-After` (1–60 giây, theo suy giảm từ tốc độ).
- `limits.go`: `ReadTimeout`/`WriteTimeout` = 60s; `IdleTimeout` 2m giữ.
- `ExtendLongLivedRequestDeadlines`: `http.ResponseController` mở deadline đọc/ghi 30 phút cho route dài: `/ws/*`, `GET /api/docker/events/stream`, `POST` compose up/down/service logs, image load/pull/pull/stream, trivy-scan, template `/api/containers/.../logs|stats/stream` (nếu có).
- `auth/rotate` dùng chung `clientIP` với bộ hạn mức tăng cấp.
- Thứ tự chuỗi trong `cmd/server/main.go` (từ trong ra): `mux` → Bearer → Extend → `LimitRequestBody` → `RequestContextTimeout` → `AuditSecuritySensitive` → `PerIPRateLimit` → `LogRequests` (req_id ở ngoài cùng).

### 03-02 — Audit (AUD-03, AUD-04)

- `internal/audit`: `Record` (JSON) + `Init`/`WriteJSON`; stdout mỗi bản ghi; tệp `~/.docklite/logs/audit.log` (append), xoay khi vượt 10MB (đổi tên cũ bằng hậu tố thời gian), xóa tệp tên bắt đầu `audit` cũ hơn 14 ngày.
- `AuditSecuritySensitive`: sau handler, ghi cho các `POST` đúng yêu cầu (prune, compose up/down, images load/pull/pull stream/remove/prune, auth rotate). `auth_status`: `none` | `invalid` (401) | `valid`. `user_agent` cắt 256 ký tự (rune).

## Kiểm thử

- Máy phát triển cần `go` trên PATH (hoặc WSL) để chạy: `go test ./...` trong `wsl-docker-service` (bài test: rate limit, `isLongLivedPath`, `isAuditedPath`).

## Ghi chú

- Audit không gói cả sự kiện 429 từ rate limit (hành vi không vào tới handler nhạy cảm); không dùng `slog` cho dòng JSON audit (cùng cấp trúc, một dòng/record).
