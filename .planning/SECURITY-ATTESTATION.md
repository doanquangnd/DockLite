# SECURITY-ATTESTATION — DockLite v2.0 IPC Hardening

Ngày attestation: 2026-04-25  
Chuẩn tham chiếu: OWASP ASVS 4.0.3 L2 (self-attestation)

## Kết luận tổng quan

- Tổng requirement v2.0: 27
- COVERED: 27
- PARTIAL: 0
- N/A: 0

## Bảng tự attest (chuẩn hóa evidence line-range)

| Requirement | Status | Evidence (file:line-range + test) |
|---|---|---|
| NET-01 | COVERED | `wsl-docker-service/cmd/server/main.go:21-22`; test `wsl-docker-service/internal/httpserver/listen_policy_test.go:11-13` |
| NET-02 | COVERED | `wsl-docker-service/cmd/server/main.go:28-31`; test `wsl-docker-service/internal/httpserver/listen_policy_test.go:13-16` |
| NET-03 | COVERED | `wsl-docker-service/internal/httpserver/register.go:30-31`; test `wsl-docker-service/internal/httpserver/openapi_test.go:25-45` |
| NET-04 | COVERED | `src/DockLite.Core/Configuration/ServiceBaseUrlSecurity.cs:16-54`; test `tests/DockLite.Tests/ServiceBaseUrlSecurityAnalyzerTests.cs:8-24` |
| NET-05 | COVERED | `wsl-docker-service/.env.example:9-14,28-33` |
| SEC-01 | COVERED | `src/DockLite.Infrastructure/Configuration/WindowsServiceApiTokenStore.cs:15-66` |
| SEC-02 | COVERED | `src/DockLite.Core/Configuration/AppSettings.cs:21-24`; test `tests/DockLite.Tests/AppSettingsStoreServiceApiTokenTests.cs:12-25` |
| SEC-03 | COVERED | `src/DockLite.Infrastructure/Configuration/AppSettingsStore.cs:112-140,179-203` |
| SEC-04 | COVERED | `wsl-docker-service/internal/httpserver/auth_rotate.go:38-46`; test `wsl-docker-service/internal/httpserver/auth_rotate_test.go:12-84` |
| SEC-05 | COVERED | `src/DockLite.App/ViewModels/SettingsViewModel.cs:1035-1070` |
| AUD-01 | COVERED | `wsl-docker-service/internal/httpserver/ratelimit_middleware.go:13-66`; test `wsl-docker-service/internal/httpserver/ratelimit_middleware_test.go:9-35` |
| AUD-02 | COVERED | `wsl-docker-service/internal/httpserver/limits.go:8-12` + `wsl-docker-service/internal/httpserver/stream_deadline_middleware.go:15-26`; test `wsl-docker-service/internal/httpserver/stream_deadline_middleware_test.go:8-24` |
| AUD-03 | COVERED | `wsl-docker-service/internal/httpserver/audit_middleware.go:35-73`; test `wsl-docker-service/internal/httpserver/audit_middleware_test.go:8-21` |
| AUD-04 | COVERED | `wsl-docker-service/internal/audit/sink.go` (rotate + retention) |
| TLS-01 | COVERED | `wsl-docker-service/internal/docklitetls/ensure.go:31-58`; test `wsl-docker-service/internal/docklitetls/ensure_test.go:10-22` |
| TLS-02 | COVERED | `wsl-docker-service/internal/docklitetls/ensure.go:138-147`; test `wsl-docker-service/internal/docklitetls/ensure_test.go:31-37` |
| TLS-03 | COVERED | `src/DockLite.App/ViewModels/SettingsViewModel.cs:900-940,1006-1014` |
| TLS-04 | COVERED | `src/DockLite.Infrastructure/Configuration/WindowsTrustedFingerprintStore.cs:18-66` |
| TLS-05 | COVERED | `src/DockLite.App/ViewModels/SettingsViewModel.cs:942-953,1017-1026` |
| TLS-06 | COVERED | `src/DockLite.App/Views/SettingsView.xaml:106-132` + `src/DockLite.App/ViewModels/SettingsViewModel.cs:881-904,984-1003` |
| PRC-01 | COVERED | `src/DockLite.Infrastructure/Wsl/WslDockerServiceAutoStart.cs:932-960` |
| PRC-02 | COVERED | `src/DockLite.Infrastructure/Wsl/WslDockerServiceAutoStart.cs:905-929`; test `tests/DockLite.Tests/WslDockerServiceAutoStartTests.cs:8-18` |
| PRC-03 | COVERED | `wsl-docker-service/scripts/run-server.sh:13-31,47-62`; `wsl-docker-service/scripts/stop-server.sh:11-41`; `wsl-docker-service/scripts/restart-server.sh:10-11` |
| PRC-04 | COVERED | test `tests/DockLite.Tests/WslDockerServiceAutoStartTests.cs:8-24` |
| PRC-05 | COVERED | test `wsl-docker-service/internal/docker/image_trivy_scan_test.go:5-20` + `wsl-docker-service/internal/compose/compose_services_validation_test.go:5-24` |
| PRC-06 | COVERED | `wsl-docker-service/internal/docker/image_trivy_scan.go:47-50,93` + `wsl-docker-service/internal/compose/compose_services.go:198,255,283` |
| PRC-07 | COVERED | tài liệu hiện tại `./.planning/SECURITY-ATTESTATION.md:1-80` |

## Lệnh xác minh

- .NET test:
  - `dotnet test tests/DockLite.Tests/DockLite.Tests.csproj`
- Go test (WSL):
  - `cd wsl-docker-service && go test ./...`
- Smoke-check PRC-01:
  - `rg -n "bash -lc .*\\{" src --type cs`

## Ghi chú phạm vi

- Đây là self-attestation theo roadmap v2.0, không thay thế pentest độc lập.
- CI supply-chain automation (SLSA/ZAP/semgrep/govulncheck) thuộc milestone sau (deferred).
