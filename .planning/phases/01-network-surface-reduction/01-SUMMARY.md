# Phase 1 — Tóm tắt thực thi

**Ngày:** 2026-04-23  
**Kế hoạch:** 01-01-PLAN (Go), 01-02-PLAN (WPF + env + docs)

## Đã giao

### Go (NET-01, NET-02, NET-03)

- Mặc định `DOCKLITE_ADDR` tương đương `127.0.0.1:17890` trong `cmd/server/main.go`.
- Nếu token rỗng và địa chỉ lắng nghe không an toàn (không loopback, hoặc unspecified), tiến trình thoát mã 2, stderr tiếng Việt.
- `internal/httpserver/listen_policy.go` + bảng kiểm thử.
- `internal/wslimit/wslimit.go`: `CheckOrigin` chặn origin lạ; `DOCKLITE_ALLOWED_ORIGINS` (danh sách phân tách dấu phẩy); `Origin` rỗng vẫn chấp nhận; test bao gồm evil.com, 127.0.0.1, allowlist.

### WPF + tài liệu (NET-04, NET-05)

- `ServiceBaseUrlSecurityAnalyzer` + enum `ServiceBaseUrlSecuritySeverity` trong `DockLite.Core`.
- `SettingsViewModel` gán mức + thông điệp; XAML dùng DataTrigger cảnh báo so với nghiêm trọng.
- `ThemeDangerForegroundBrush` (sáng/tối).
- `wsl-docker-service/.env.example` và `docs/docklite-lan-security.md` đồng bộ mặc định loopback + token + allowlist.

## Kiểm thử

- WSL: `go test ./...` (thư mục `wsl-docker-service`) — pass.
- Windows: `dotnet test tests/DockLite.Tests/DockLite.Tests.csproj -c Release` — 51 test pass (gồm `ServiceBaseUrlSecurityAnalyzerTests`).

## Ghi chú

- Máy phát triển Windows có thể không có `go` trên PATH; dùng WSL để chạy `go test` như trên.
