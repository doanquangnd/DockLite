# Phase 4: TLS Opt-in với TOFU — tóm tắt thực thi

**Ngày:** 2026-04-23

## Đã giao

### Go (wsl-docker-service)

- Gói `internal/docklitetls`: tạo hoặc tái sử dụng cặp `cert.pem` / `key.pem` trong `~/.docklite/tls`, ECDSA P-256, CN `DockLite WSL Service`, SAN gồm 127.0.0.1, ::1, hostname, IP từ giao diện mạng, và `DOCKLITE_TLS_EXTRA_SAN` (danh sách phân tách bằng dấu phẩy). Key `chmod 0600` trên Unix. TLS 1.2 tối thiểu.
- `cmd/server/main.go`: nếu `DOCKLITE_TLS_ENABLED=true` thì `ListenAndServeTLS`; ngược lại `ListenAndServe`.
- Bài kiểm tra đơn vị: `internal/docklitetls/ensure_test.go`.
- `.env.example`: bình luận về `DOCKLITE_TLS_ENABLED` và `DOCKLITE_TLS_EXTRA_SAN`.

### WPF / .NET

- Core: `ITrustedFingerprintStore`, `TlsCertificateDisplayInfo`, `TlsCertificateFingerprint` (SHA-256 DER cert, dạng hex có dấu hai chấm).
- Infrastructure: `WindowsTrustedFingerprintStore` (resource `DockLite:TrustedFingerprint:` + `Uri.EscapeDataString(host)` + cổng), `DockLiteTlsClientValidation`, `TlsServerCertificateProber` (SslStream để lấy cert trước khi pin).
- `DockLiteHttpSession`: `HttpClientHandler` với `ServerCertificateCustomValidationCallback` khi Base URL là https và có store; `ApplyTlsToClientWebSocketIfNeeded` cho wss; WebSocket: callback nhận `object` rồi cast sang `Uri` (API .NET 8).
- `LogStreamClient` / `StatsStreamClient`: gọi cấu hình WebSocket TLS.
- `IDialogService` + `WpfDialogService`: hộp thoại TOFU và cert đổi (`MessageBoxImage.None`, Yes/No).
- `SettingsViewModel` + `SettingsView.xaml`: checkbox «Bật TLS», hiển thị pin, nút «Kết nối và lưu pin TLS», «Quên pin TLS»; chuỗi i18n vi/en.
- `AppShellFactory`: tạo `WindowsTrustedFingerprintStore` và truyền vào `DockLiteHttpSession` và `SettingsViewModel`.

## Bằng chứng

- Build: `dotnet build` project App (Windows).
- Test: `dotnet test` `DockLite.Tests` (52 passed).
- Go: cần môi trường có `go` (ví dụ WSL): `go test ./internal/docklitetls/...`

## Ghi chú hành vi

- HTTP(S) / WSS: chỉ chấp nhận cert khi fingerprint khớp pin trong Credential. Không pin → mọi kết nối https/wss tới cert tự ký bị từ chối cho đến khi dùng «Kết nối và lưu pin TLS».
- Lần đầu / đổi cert: hộp thoại qua probe trong Cài đặt (không auto-popup trên mọi lỗi SSL giữa chừng).

## Việc còn lại (ngoài phase)

- Phase 5: Process Hardening theo ROADMAP.
- Tùy chọn: bổ sung kiểm thử tích hợp TLS (openssl/curl) trong CI khi có Linux agent.
