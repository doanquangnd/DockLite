# Phase 2 — Tóm tắt thực thi

**Ngày:** 2026-04-23  
**Kế hoạch:** 02-01, 02-02, 02-03 (gộp trong một lần giao mã)

## Đã giao

### WPF / .NET (SEC-01, SEC-02, SEC-03, SEC-05)

- `IServiceApiTokenStore` (Core) và `WindowsServiceApiTokenStore` (PasswordVault, resource `DockLite:ServiceApiToken:{profile}`).
- `AppSettings` thêm `ServiceApiTokenProfile` (lưu JSON); `ServiceApiToken` có `[JsonIgnore]`.
- `AppSettingsStore` nhận `IServiceApiTokenStore`; `Save` ghi bí mật qua kho; `Load` hợp nhất; migration từ JSON legacy (Pascal + camel) + `AppFileLog`.
- `AppShellFactory` tạo kho + store; TFM `net8.0-windows10.0.17763.0` (App, Infrastructure, Tests).
- `SettingsViewModel`: gọi `POST /api/auth/rotate` qua `IDockLiteApiClient`, dòng trạng thái credential, nút xoay token.
- Hợp đồng: `AuthRotateRequest`, `AuthRotateData`; `DockLiteErrorCodes` bổ sung `RateLimit`, `Auth`.
- `DockLiteApiClient.RotateServiceApiTokenAsync`.
- Giao diện: lưới token + nút, chuỗi `Ui_Settings_Conn_ApiTokenRotate*`; cập nhật tooltip token.

### Go (SEC-04)

- `MutableToken` (mutex, `NewMutableToken`, `Update`, `CompareBytes`, `BytesCopy`).
- `RequireBearerToken(*MutableToken, ...)`; `main` luôn bọc; route `POST /api/auth/rotate` khi bật token lúc khởi động.
- `HandleAuthRotate`: rate 5/phút/IP, thân tối đa 4 KiB, trả `new_token` (hex 64 ký tự từ 32 byte ngẫu nhiên).
- `apiresponse`: mã `AUTH`, `RATE_LIMIT`.
- `openapi.json` cập nhật path + schema.
- Test: `auth_rotate_test.go`; `integration` gọi `Register(mux, nil)`.

## Kiểm thử

- Windows: `dotnet test` (DockLite.Tests) — 52 test, gồm serialize không lộ token.
- WSL: `go test ./internal/httpserver/...` (cần `go` trên PATH, thường dùng WSL).

## Ghi chú

- Khởi động lại `docklite-wsl` đọc lại `DOCKLITE_API_TOKEN` từ môi trường; sau khi xoay, process giữ mật khẩu mới trong RAM cho đến khi kết thúc. Cập nhật biến môi trường WSL nếu cần đồng bộ khi restart service.
