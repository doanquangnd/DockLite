# Phase 5: Process Hardening — tóm tắt thực thi

Ngày hoàn tất: 2026-04-25

## Kết quả chính

- Hoàn tất PRC-01..PRC-07.
- Đóng milestone v2.0 IPC Hardening (13/13 kế hoạch).

## Chi tiết triển khai

### 05-01 (WPF/C#)

- `WslDockerServiceAutoStart.SpawnWslLifecycleScript` chuyển sang native argv:
  - `wsl.exe [-d <distro>] --cd <unixPath> -- bash <script>`
  - bỏ nội suy `bash -lc "cd ..."` trong luồng spawn lifecycle.
- Thêm `ValidateWslUnixPathForSpawn()`:
  - reject: `'`, `"`, `` ` ``, `$`, `\n`, `\r`, `;`, `|`, `&`, `<`, `>`.
  - ném `ArgumentException` nêu ký tự vi phạm.
- Thêm test: `tests/DockLite.Tests/WslDockerServiceAutoStartTests.cs`
  - case path chứa `'`
  - case path chứa `$(...)`
  - case path chứa newline.

### 05-02 (Go + scripts)

- `scripts/run-server.sh`, `scripts/stop-server.sh`, `scripts/restart-server.sh` chuyển sang PID file:
  - `~/.docklite/run/docklite-wsl.pid`
  - stop: `TERM`, chờ tối đa 5 giây, sau đó `KILL` nếu còn sống.
  - bỏ hoàn toàn `pgrep/pkill -f`.
- `internal/docker/image_trivy_scan.go`:
  - reject `imageRef` bắt đầu bằng `-`.
  - ép `--` trước `imageRef` khi gọi trivy.
- `internal/compose/compose_services.go`:
  - reject service name bắt đầu bằng `-`.
  - thêm `--` trước service ở `exec/logs/start/stop`.
- Thêm test:
  - `internal/docker/image_trivy_scan_test.go`
  - `internal/compose/compose_services_validation_test.go`

### 05-03 (docs)

- Tạo `SECURITY-ATTESTATION.md` map toàn bộ 27 requirement v2.0 với trạng thái/evidence.

## Kiểm thử

- Chạy `dotnet test tests/DockLite.Tests/DockLite.Tests.csproj`:
  - Passed: 56
  - Failed: 0
- Go test cần chạy trong môi trường có Go (WSL/Linux):
  - `go test ./...` trong `wsl-docker-service`.

## Ghi chú

- Mục grep tiêu chí phase (`rg -n "bash -lc .*{"`) cho file C# không còn match trong logic spawn lifecycle.
- Các script lifecycle hiện fail-safe theo PID file, tránh dừng nhầm process khác.
