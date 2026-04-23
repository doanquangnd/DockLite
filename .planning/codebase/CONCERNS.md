# Codebase Concerns

**Analysis Date:** 2026-04-23

**Phạm vi:** Audit Zero-Trust toàn bộ repo DockLite (WPF `.NET 8` + service Go `docklite-wsl` chạy trong WSL + scripts PowerShell/bash + `.github/workflows/`). Các phát hiện được xếp theo ba mức: CRITICAL, SUSPICIOUS, WEAK PRACTICE. Mỗi mục luôn kèm đường dẫn file và dòng (backtick) để điều hướng nhanh.

**Tham chiếu chéo tài liệu hiện có:**
- `docs/docklite-lan-security.md` — cảnh báo chính thức về rủi ro LAN/HTTP không TLS và token API.
- `docs/01_docker_gui_wpf_go_wsl_analysis.md`, `docs/03_docklite-optimization-and-extensions.md`, `docs/05_docklite-system-analysis-and-improvements.md` — phân tích kiến trúc/ưu tiên nâng cấp.
- `docs/04_docklite-release-sbom.md` — quy trình release/SBOM/Cosign.

---

## Tech Debt

### CRITICAL — Mặc định bind `0.0.0.0:17890` (service Go phơi ra LAN)

- Mục: Service không giới hạn loopback theo mặc định; mọi máy trong LAN có thể truy cập khi chưa cấu hình tường lửa.
- Files:
  - `wsl-docker-service/cmd/server/main.go` dòng 15–18 (hard-code `"0.0.0.0:17890"` khi `DOCKLITE_ADDR` trống).
  - `wsl-docker-service/.env.example` dòng 8–9 (ví dụ cấu hình cũng gợi ý `0.0.0.0:17890`).
  - `src/DockLite.App/Help/PageHelpTexts.cs` dòng 188 và `src/DockLite.App/Resources/UiStrings.vi.xaml` dòng 162 (UI mô tả base URL mặc định `http://127.0.0.1:17890/` — mâu thuẫn với bind mặc định `0.0.0.0`).
  - `docs/docklite-lan-security.md` (đã nêu rủi ro nhưng không enforce).
- Impact: Client mặc định nói localhost trong khi server mặc định lắng nghe mọi interface. Bất kỳ ai trong LAN biết IP WSL đều có thể gọi API Docker (nếu `DOCKLITE_API_TOKEN` trống = full quyền quản trị Docker). HTTP không TLS nên dễ bị nghe lén/MITM.
- Fix approach:
  - Đổi mặc định sang `127.0.0.1:17890` trong `main.go`; yêu cầu người dùng chủ động opt-in LAN qua `DOCKLITE_ADDR=0.0.0.0:17890` và khi đó BẮT BUỘC token (fail-closed nếu token trống và addr != localhost).
  - In cảnh báo `slog.Warn` khi addr không phải loopback và token chưa đặt.
  - Cập nhật `.env.example` để khuyến nghị `127.0.0.1:17890` mặc định.

### CRITICAL — Shell injection tiềm ẩn khi spawn `wsl.exe bash -lc`

- Mục: `wslUnixPath` được nội suy trực tiếp vào chuỗi `bash -lc` với single-quote context nhưng không escape dấu nháy đơn.
- Files:
  - `src/DockLite.Infrastructure/Wsl/WslDockerServiceAutoStart.cs` dòng 905: `string inner = $"cd '{wslUnixPath}' && exec bash {scriptRelativeFromRoot}";`
  - Đối chiếu: hàm `BashSingleQuoted` đã có sẵn (dòng 567–570) và được dùng đúng ở `TryReadWslDestinationVersionForSyncAsync` (dòng 583–584) — nhưng KHÔNG được dùng ở đây.
- Impact:
  - Nếu `wslUnixPath` chứa ký tự `'` (ví dụ thư mục tên `a' && rm -rf ~ && echo '`), nội dung sẽ thoát khỏi context quote và chạy lệnh tùy ý trong WSL. `wslUnixPath` có thể đến từ output `wsl.exe wslpath -a` (tin cậy hệ điều hành) HOẶC từ trường `WslDockerServiceWindowsPath` / `WslDockerServiceSyncSourceWindowsPath` trong `settings.json`.
  - `settings.json` nằm trong `%LocalAppData%\DockLite\settings.json` (xem `AppSettingsStore.cs` dòng 20–23) — là plaintext, dễ ghi đè bởi malware hoặc tool đồng bộ backup.
  - `scriptRelativeFromRoot` là chuỗi hard-coded (an toàn) nhưng `wslUnixPath` thì không.
- Exploit scenario (local privilege escalation / persistence):
  1. Malware trên Windows ghi settings.json với `WslDockerServiceWindowsPath` dạng đường dẫn UNC chứa `'`.
  2. Khi DockLite khởi động lại, `SpawnWslLifecycleScript` chạy payload bên trong WSL của người dùng (truy cập được Docker socket → thoát container → root-on-host).
- Fix approach:
  - Dùng `BashSingleQuoted(wslUnixPath)` thay cho `'{wslUnixPath}'`.
  - Hoặc bỏ shell hoàn toàn: truyền thẳng `cd` và `exec bash <script>` như nhiều đối số riêng biệt cho `wsl.exe` mà không dùng `bash -lc "..."`. Ví dụ gọi `wsl.exe -d <distro> --cd <wslUnixPath> -- bash scripts/run-server.sh` (WSL2 hỗ trợ `--cd`).
  - Thêm validation nghiêm ngặt trên `wslUnixPath`: reject ký tự `'`, `"`, `` ` ``, `$`, `\n`, `\r`, `;`, `|`, `&`.

### CRITICAL — Token API lưu plaintext trong `%LocalAppData%\DockLite\settings.json`

- Files:
  - `src/DockLite.Infrastructure/Configuration/AppSettingsStore.cs` dòng 18–23, 85–90 (serialize AppSettings thẳng ra JSON, không DPAPI).
  - `src/DockLite.Core/Configuration/AppSettings.cs` dòng 14–17 (`ServiceApiToken`).
  - `src/DockLite.App/ViewModels/SettingsViewModel.cs` dòng 861, 1028 (save token raw vào settings).
- Impact: Bất kỳ process nào chạy dưới cùng user Windows (malware, tool backup, OneDrive sync) đều đọc được token. Token dài 64 hex → nếu lộ + service bind 0.0.0.0 → attacker điều khiển Docker daemon từ xa.
- Fix approach:
  - Dùng `ProtectedData.Protect(...)` (DPAPI, scope `CurrentUser`) cho `ServiceApiToken` trước khi ghi ra disk; giải mã khi load.
  - Hoặc dùng Windows Credential Manager (`Windows.Security.Credentials.PasswordVault` / `CredMan`) cho secrets, tách khỏi file cài đặt thường.
  - Xóa token khỏi copy export trong `ExportToCopy` (`AppSettingsStore.cs` dòng 29–49) hoặc ít nhất cảnh báo người dùng trước khi lưu file sao lưu chứa token.

### SUSPICIOUS — WebSocket `CheckOrigin: true` (mở Origin)

- Files: `wsl-docker-service/internal/wslimit/wslimit.go` dòng 32: `CheckOrigin: func(r *http.Request) bool { return true }`.
- Impact (Zero-Trust):
  - Trình duyệt bất kỳ có thể mở `ws://<host>:17890/ws/containers/<id>/logs` từ web page độc hại nếu người dùng đang ở cùng LAN.
  - Khi `DOCKLITE_API_TOKEN` trống: CSWSH thành công hoàn toàn → attacker có thể theo dõi logs container.
  - Khi token bật: JS browser không set được header `Authorization`, nên browser CSWSH không pass auth. Nhưng một client native (curl/non-browser) vẫn pass. Vẫn là defense-in-depth cần siết.
- Fix approach:
  - Mặc định chặn Origin ngoài `http://localhost`, `http://127.0.0.1`, `http://[::1]` (và tùy chọn whitelist qua env `DOCKLITE_ALLOWED_ORIGINS`).
  - Nếu không cần gọi từ browser, chỉ cho phép request không có `Origin` (client non-browser) + bearer token.

### SUSPICIOUS — Argument injection vào `trivy image <ref>`

- Files: `wsl-docker-service/internal/docker/image_trivy_scan.go` dòng 57–83.
  - `body.ImageRef` chỉ kiểm tra length < 512 và trim space; không kiểm tra ký tự bắt đầu `-`.
  - Dòng 82–83: `args = append(args, ref); cmd := exec.CommandContext(ctx, "trivy", args...)`.
- Impact: Client có quyền gọi API có thể gửi `ref = "--force"` hoặc `"--cache-dir=/var/lib/trivy"` v.v. để lừa trivy nuốt flag thay vì image reference → tối thiểu là thay đổi hành vi, tối đa là ghi/đọc file tùy `--output` / `--policy`. Không phải RCE trực tiếp (vì không qua shell) nhưng là misuse interface. Tương tự `body.PolicyPath` có kiểm tra `..` nhưng cũng nối `--policy <pp>` — attacker có thể truyền `/path/to/malicious/policy.rego` ghi từ một API khác.
- Fix approach: Reject `ref` bắt đầu bằng `-`; hoặc thêm `"--"` vào args trước `ref` để stop flag parsing.

### SUSPICIOUS — Argument injection vào `docker compose exec/start/stop/logs`

- Files: `wsl-docker-service/internal/compose/compose_services.go` dòng 166–223, 256–299; `wsl-docker-service/internal/compose/compose_docker_args.go` dòng 9–27.
  - `validateComposeServiceName` (dòng 34–46) và `parseExecCommandParts` (dòng 199–223) chặn ký tự shell metachar nhưng KHÔNG reject tên bắt đầu bằng `-`.
  - `dockerComposeArgs` nối thẳng `--profile <name>` và tên service/command parts mà không `--` separator.
- Impact: Client có auth có thể gửi `service = "--help"` hoặc `service = "--env"` làm docker compose hiểu nhầm flag. Tên profile cũng vậy. Không phải RCE qua shell (không dùng shell), nhưng có thể khiến docker compose làm hành vi khác mong đợi (ví dụ `exec -T --env KEY=val svc` nếu attacker khéo ghép).
- Fix approach: Thêm `"--"` trước các argument người dùng; reject leading `-` trong `validateComposeServiceName` và profile; whitelist charset (a-zA-Z0-9_.-) cho tên.

### SUSPICIOUS — Compose project store có thể bị ghi đè → chạy `docker compose` ở thư mục tùy ý

- Files: `wsl-docker-service/internal/compose/compose.go` dòng 143–216 (lưu `~/.docklite/compose_projects.json` với chmod 0o644), dòng 486–500 (`cmd.Dir = proj.WslPath`).
- Impact: Người dùng khác trên cùng WSL user (hoặc process chạy dưới cùng user) ghi đè `compose_projects.json` với `wslPath` trỏ tới bất kỳ thư mục nào có `docker-compose.yml` độc hại (curl pipe payload, `image:` tùy ý, mount `/`). Khi DockLite gọi `/api/compose/up`, service sẽ `docker compose up -d` tại thư mục đó → cấp quyền Docker cho payload.
- Fix approach:
  - Kiểm tra permission store file (0600), đặt `os.WriteFile(..., 0o600)`.
  - Validate lại `wslPath` trước mỗi lần exec (đã có `validateComposeProjectDirOrFiles` nhưng chỉ khi ADD/PATCH, không lúc UP).
  - Thêm hash/HMAC danh sách project (ký bằng key trong `settings.json`) để phát hiện tampering.

### WEAK PRACTICE — Port/host hard-coded và lỏng lẻo

- Files:
  - `wsl-docker-service/cmd/server/main.go` dòng 16: hard-code `"0.0.0.0:17890"`.
  - `wsl-docker-service/.env.example` dòng 9: gợi ý 0.0.0.0.
  - `src/DockLite.Core/Configuration/` — `DockLiteApiDefaults.DefaultPort` và nhiều nơi tham chiếu 17890 (xem `DockLiteHttpSession.cs` dòng 21 comment, `Start-DockLiteWsl.ps1` dòng 27).
- Impact: Khi port 17890 xung đột hoặc bị tool khác hijack, app khó phát hiện.
- Fix approach: Centralize port default + cho phép override; in warning khi bind fail.

### WEAK PRACTICE — File lớn, khó audit (complexity risk)

| File | Dòng |
|---|---|
| `src/DockLite.App/ViewModels/ContainersViewModel.cs` | 1499 |
| `src/DockLite.App/ViewModels/ImagesViewModel.cs` | 1059 |
| `src/DockLite.Infrastructure/Wsl/WslDockerServiceAutoStart.cs` | 1017 |
| `src/DockLite.App/ViewModels/SettingsViewModel.cs` | 998 |
| `src/DockLite.App/ViewModels/ComposeViewModel.cs` | 958 |
| `src/DockLite.App/ViewModels/ShellViewModel.cs` | 748 |

- Impact: File 1000+ dòng khiến code review khó nhận biết nhánh ẩn (ví dụ `SpawnWslLifecycleScript` ở dòng 900+ có shell injection — nếu split thành module riêng thì bug dễ phát hiện).
- Fix approach: Tách partial class theo domain (Compose, ImagesPull, TokenHandling); ưu tiên `WslDockerServiceAutoStart.cs` vì tập trung toàn bộ logic spawn WSL.

---

## Known Bugs

### SUSPICIOUS — Rò rỉ `Process` trong danh sách static `WslRunServerProcesses`

- Files: `src/DockLite.Infrastructure/Wsl/WslDockerServiceAutoStart.cs` dòng 40–42, 987–990.
- Symptoms: Mỗi lần người dùng Start/Restart/Build → tạo Process mới, thêm vào list, nhưng không có logic loại bỏ sau `Exited`. Sau nhiều phiên, list phình to — giữ handle OS (mặc dù nhẹ) và bộ nhớ.
- Trigger: Mở UI, vào Cài đặt, nhấn Restart/Build nhiều lần.
- Workaround: Chưa có. Không gây crash nhanh, nhưng trên phiên dài (app luôn mở) là memory leak.
- Fix approach: Gắn `p.Exited` handler dọn khỏi list; hoặc thay list bằng `ConcurrentBag<WeakReference<Process>>`.

### WEAK PRACTICE — `ReadTimeout` / `WriteTimeout` 30 phút

- Files: `wsl-docker-service/internal/httpserver/limits.go` dòng 10–13.
- Symptoms: Với auth, slow-loris khó; nhưng khi `DOCKLITE_API_TOKEN` trống VÀ bind 0.0.0.0, mỗi client LAN có thể giữ socket 30 phút với payload nhỏ → DoS các slot `net/http`.
- Fix approach: Nếu auth tắt, dùng timeout ngắn (e.g. 60s) cho mọi endpoint trừ `/api/images/load`, `/api/compose/*` (vốn đã có `RequestContextTimeout` riêng).

### WEAK PRACTICE — Integration test CI dùng Docker thật không kiểm soát

- Files: `.github/workflows/ci.yml` dòng 38–52.
- Symptoms: Job `go-integration` chạy trên `ubuntu-latest` runner với Docker có sẵn, nhưng không pin image cụ thể; test `TestAPIDockerInfoWithEngine` skip nếu Docker unavailable — điều này có thể che giấu lỗi thực trong CI.
- Fix approach: Thêm step `docker pull <pinned>` trước test; fail job nếu Docker.Ping không thành công (thay vì skip âm thầm).

---

## Security Considerations

### CRITICAL — Supply-chain: `curl | sh` cài Syft từ `main` branch trong release

- Files: `.github/workflows/release.yml` dòng 30–33.
  - Dòng 32: `curl -sSfL https://raw.githubusercontent.com/anchore/syft/main/install.sh | sh -s -- -b /usr/local/bin`.
- Risk: Nếu anchore/syft main branch bị compromise (chèn malware vào install.sh), runner GitHub Actions (có `contents: write` + `id-token: write`) sẽ thực thi payload với quyền:
  - Ghi Release (đính kèm binary giả).
  - Ký Cosign keyless (binary giả có chữ ký hợp lệ — người dùng tải về tin tưởng).
  - Đọc `GITHUB_TOKEN` với quyền write.
- Current mitigation: Có `-sSfL` bảo đảm TLS nhưng không kiểm tra checksum của install.sh.
- Fix approach:
  - Dùng action chính thức `anchore/sbom-action@<SHA>` pin theo commit SHA.
  - Hoặc tải syft binary từ GitHub Release pinned version + verify SHA256 + Cosign signature (syft được sign).
  - Không bao giờ pipe shell script remote vào `sh` trong CI có quyền ghi release.

### CRITICAL — Quyền của GitHub Actions: `contents: write` + `id-token: write`

- Files: `.github/workflows/release.yml` dòng 9–11.
- Risk: Kết hợp với việc `curl | sh` (mục trên) và third-party actions pin theo tag (không SHA), một tag moved hoặc maintainer compromised có thể chiếm quyền ký release/push tag.
  - `actions/checkout@v4`, `actions/setup-go@v5`, `actions/setup-dotnet@v4` (first-party, rủi ro thấp).
  - `sigstore/cosign-installer@v3.7.0` (tag) — first-party Sigstore.
  - `softprops/action-gh-release@v2` (third-party, tag) — nhận `GITHUB_TOKEN`, publish release. Nếu v2 tag bị force-push bởi attacker kiểm soát repo này, payload sẽ chạy trong môi trường release.
- Fix approach:
  - Pin mọi action bằng SHA tuyệt đối (Dependabot có thể auto-update):
    ```yaml
    uses: softprops/action-gh-release@<40-char-sha>  # v2.x
    ```
  - Bật GitHub native attestation (`actions/attest-build-provenance@<sha>`) kèm OIDC thay cho third-party signing.

### CRITICAL — Docker socket = root. Bất kỳ ai có token = root-on-host

- Files: `wsl-docker-service/internal/dockerengine/client.go` dòng 17–25 (singleton kết nối Docker socket via env mặc định); `wsl-docker-service/internal/httpserver/register.go` (hầu hết endpoint đều chạm Docker).
- Risk: Docker API tương đương root Linux (có thể `docker run -v /:/host --privileged ...`). Do đó ai đến được endpoint `POST /api/containers/...`, `/api/images/pull`, `/api/compose/up` = leo thang root trên host WSL.
  - Khi service bind 0.0.0.0 + token trống → bất kỳ ai trên LAN = root.
  - Khi token bật và lộ → cùng hậu quả.
- Current mitigation: Token Bearer với `subtle.ConstantTimeCompare` (`auth.go` dòng 44–49) — đúng về mặt timing.
- Fix approach:
  - Fail-closed: nếu `addr` không phải loopback VÀ token trống → service từ chối khởi động và in hướng dẫn (xem khuyến nghị trên).
  - Cân nhắc bổ sung cơ chế whitelist IP ở tầng service (env `DOCKLITE_ALLOWED_CIDRS`) để defense-in-depth, không phụ thuộc tường lửa Windows.
  - Ghi chú rõ trong README: đừng expose service qua port-forward router.

### SUSPICIOUS — API token in plaintext qua HTTP không mã hóa (đã biết, nhắc lại)

- Files: `docs/docklite-lan-security.md` đã cảnh báo; `src/DockLite.Core/Configuration/HttpClientAppSettings.cs` dòng 27–32 gán `Authorization: Bearer <token>` trên mọi request HTTP.
- Risk: Trên LAN không tin cậy, token gửi rõ; sniff một lần → attacker dùng được từ xa.
- Fix approach: Yêu cầu TLS khi `addr != loopback` (mount reverse proxy Caddy/nginx trong hướng dẫn); thêm cảnh báo trên UI khi `ServiceBaseUrl` không phải `http://127.0.0.1` / `https://` (`SettingsViewModel.cs` dòng 1076–1084 đã có warning — tốt, nhưng không ép buộc).

### SUSPICIOUS — Settings file không có cờ `FILE_ATTRIBUTE_NOT_CONTENT_INDEXED` / permissive ACL

- Files: `src/DockLite.Infrastructure/Configuration/AppSettingsStore.cs` dòng 85–90: `File.WriteAllText(_filePath, json)`.
- Risk: File cài đặt (kèm token plaintext) có ACL thừa kế từ `%LocalAppData%`. Windows search indexer và backup tool có thể đọc/đồng bộ ra cloud.
- Fix approach:
  - Đặt ACL chỉ cho user hiện tại (`FileSystemAccessRule` deny SYSTEM/others) — hoặc ít nhất mã hóa DPAPI (như mục CRITICAL về token plaintext).
  - Thêm attribute `NotContentIndexed` khi tạo thư mục.

### SUSPICIOUS — Compose project `wslPath` không re-validated trước exec

- Files: `wsl-docker-service/internal/compose/compose.go` dòng 486–500; cũng `compose_services.go` dòng 284–299.
- Risk (đã nêu ở Tech Debt): Attacker local ghi đè store → service chạy `docker compose` tại thư mục chứa `docker-compose.yml` độc (có `image:` kéo image backdoor, `command:` chạy payload, `volumes:` mount `/`). Docker compose không bị sandbox.
- Fix approach: Trước mỗi lần exec, gọi lại `validateComposeProjectDirOrFiles(proj.WslPath, proj.ComposeFiles)` (hiện chỉ gọi khi ADD).

### SUSPICIOUS — `UseShellExecute = true` khi mở URI/file

- Files:
  - `src/DockLite.App/Services/WpfDialogService.cs` dòng 115–130 (`OpenUriInBrowser`).
  - `src/DockLite.App/ViewModels/SettingsViewModel.cs` dòng 541–556 (`OpenLanSecurityDoc`).
- Risk hiện tại: Chuỗi truyền vào là `Uri` (từ hyperlink) hoặc `_lanSecurityMarkdownPath` (được compute nội bộ từ `LanSecurityDocPaths.TryResolve`). Không nhận input người dùng trực tiếp → hiện chưa khai thác được.
- Fix approach (hardening):
  - Giới hạn scheme cho `OpenUriInBrowser`: chỉ cho `http`, `https`, `file`. Reject `javascript:`, `ms-*:`, `shell:`, v.v.
  - Không bao giờ truyền chuỗi người dùng nhập (ví dụ input từ TextBox) vào `ProcessStartInfo` với `UseShellExecute = true`.

### WEAK PRACTICE — `openapi.json` 21KB commit trong repo cạnh code handler

- Files: `wsl-docker-service/internal/httpserver/openapi.json` (21838 bytes).
- Risk: File lớn phản ánh thủ công toàn bộ API surface. Nếu lệch khỏi handler thật, ứng dụng/third-party có thể gọi endpoint không tồn tại (nhiễu) hoặc thiếu endpoint được mô tả (thiếu audit trail). Không phải lỗ hổng trực tiếp nhưng tăng attack surface theo hướng mô tả không khớp implementation → dễ bỏ sót khi security review.
- Fix approach: Generate openapi.json từ code (Go struct tags, ví dụ `swaggo/swag`) trong `go generate` và chạy diff trong CI để chặn drift.

### WEAK PRACTICE — `.gitignore` exclude `docs/*.md`

- Files: `.gitignore` dòng 31 — `docs/*.md`.
- Risk: Thư mục `docs/` chứa cả tài liệu bảo mật (`docklite-lan-security.md`, `04_docklite-release-sbom.md`). Dòng ignore này khiến tệp tài liệu mới không được track → developer clone mới không thấy; nguy hiểm hơn, attacker commit qua LFS/orphan path không bị ai soi.
- Fix approach: Xóa dòng `docs/*.md` khỏi `.gitignore` (hoặc chuyển sang ignore pattern chính xác, ví dụ `docs/local-*.md`).

### WEAK PRACTICE — Không rate-limit HTTP/WebSocket

- Files: `wsl-docker-service/internal/httpserver/register.go`, `wsl-docker-service/internal/httpserver/limits.go`.
- Risk: Không có giới hạn số request/phút. `/api/containers/top-by-cpu`, `/api/containers/stats-batch` tốn CPU. Không có auth → DoS; có auth + token lộ → DoS.
- Fix approach: Thêm middleware rate-limit (ví dụ `golang.org/x/time/rate` đã là dep indirect) 30 req/s/IP cho REST, 2 upgrade/s/IP cho WS.

### WEAK PRACTICE — Origin "*" trên WebSocket kết hợp bind 0.0.0.0 (defense-in-depth)

- Files: đã liệt kê ở CRITICAL + SUSPICIOUS mục `CheckOrigin`.
- Risk lặp lại ở đây với focus defense-in-depth: ngay khi token có, vẫn nên siết Origin để browser hoàn toàn không connect tới.

---

## Performance Bottlenecks

### WEAK PRACTICE — Đọc `/api/containers` trả toàn bộ container (`All: true`)

- Files: `wsl-docker-service/internal/docker/containers.go` dòng 51–97.
- Problem: Host nhiều container (100+) → response lớn, WPF parse chậm, UI grid lag. Đã có endpoint top-by-memory/cpu nhưng list vẫn không phân trang.
- Cause: `container.ListOptions{All: true}` không filter/paginate.
- Improvement: Thêm query param `limit`, `since`, `filter` (đã có trong Docker Engine API); paginate phía WPF.

### WEAK PRACTICE — `stdcopy.StdCopy` ghi log buffer vào memory (WebSocket logs)

- Files: `wsl-docker-service/internal/ws/logs.go` dòng 73–103.
- Problem: Với container xuất log tốc độ cao, goroutine đọc `conn.ReadMessage()` không back-pressure. `textWriter.Write` dùng `sync.Mutex` cho mỗi dòng; nếu client chậm đọc, daemon Docker có thể bị block. `wslimit` chỉ giới hạn số kết nối, không giới hạn bytes/s.
- Improvement: Thêm `SetWriteDeadline` trên mỗi `WriteMessage`; đóng kết nối khi vượt timeout.

### WEAK PRACTICE — `json.MarshalIndent` mỗi lần save compose projects

- Files: `wsl-docker-service/internal/compose/compose.go` dòng 199–217.
- Problem: Thao tác save toàn bộ store sau mỗi mutation (Add/Patch/Delete). Khi có nhiều project, disk IO không cần thiết.
- Improvement: Debounce save 200ms; hoặc `json.Marshal` (không indent) — file nhỏ hơn.

---

## Fragile Areas

### SUSPICIOUS — Life-cycle WSL process phức tạp (`WslDockerServiceAutoStart`)

- Files: `src/DockLite.Infrastructure/Wsl/WslDockerServiceAutoStart.cs` (1017 dòng).
- Why fragile:
  - Nhiều đường dẫn spawn (`TryStartServiceManually`, `TryStopServiceManually`, `TryBuildServiceManually`, `TryRestartServiceManually`, `TrySyncWindows...`) — 5 nhánh, mỗi nhánh có probe/health/retry riêng.
  - Gọi `wsl.exe` đồng bộ + bất đồng bộ, bắt `p.ExitCode`, đọc stdout/stderr qua callback (memory leak ở `WslRunServerProcesses`).
  - Ghép shell `bash -lc` (nguy cơ inject — xem CRITICAL).
- Safe modification:
  - Luôn dùng `BashSingleQuoted` (đã có) khi chèn path vào `bash -lc`.
  - Viết unit test cho `SpawnWslLifecycleScript` mô phỏng path chứa ký tự nguy hiểm.
- Test coverage gaps: Không có test cho `SpawnWslLifecycleScript`, `TryRestartServiceManually`, `TrySyncWindowsSourceToLinuxDestinationAsync` (kiểm tra trong `tests/` thấy chỉ có unit test cho các helper nhỏ).

### SUSPICIOUS — `pkill -f "bin/docklite-wsl"` match quá rộng

- Files: `wsl-docker-service/scripts/restart-server.sh` dòng 11–13, `wsl-docker-service/scripts/stop-server.sh` dòng 11–13.
- Why fragile: `pkill -f` match toàn bộ command line. Nếu người dùng chạy `less /tmp/bin/docklite-wsl.log` → bị giết nhầm. Hiếm nhưng có khả năng.
- Safe modification: Lưu PID (`$!`) từ `run-server.sh` vào `.pid` file; stop/restart đọc pid đó.

### SUSPICIOUS — Normalize URL scheme không ép HTTPS

- Files: `src/DockLite.Core/Configuration/ServiceBaseUriHelper.cs` dòng 12–23; `src/DockLite.App/ViewModels/SettingsViewModel.cs` dòng 1076–1084 (warning khi host không loopback, nhưng KHÔNG warning khi scheme là `http://` thay vì `https://`).
- Why fragile: Người dùng tưởng an toàn vì thấy "127.0.0.1" nhưng thực ra vẫn HTTP.
- Safe modification: Warning khi scheme != `https` VÀ host != loopback.

---

## Scaling Limits

### WEAK PRACTICE — `DOCKLITE_WS_MAX_CONNECTIONS` mặc định 64, tối đa 4096

- Files: `wsl-docker-service/internal/wslimit/wslimit.go` dòng 17–28.
- Current: 64 connection. Mỗi container có 2 stream (logs + stats) → 32 container đồng thời đã sát limit.
- Limit: 4096 hard-cap (hợp lý).
- Scaling path: Multiplex nhiều stream qua 1 kết nối (subscription model) thay vì 1 WS/stream.

### WEAK PRACTICE — `MaxBodyBytesPOST = 64 MiB` có thể chặn request compose lớn

- Files: `wsl-docker-service/internal/httpserver/limits.go` dòng 16.
- Current: POST/PATCH/PUT giới hạn 64MB (trừ `/api/images/load` 512MB).
- Limit: Payload compose siêu lớn (cấu hình 100 service) không load được.
- Scaling path: Cho phép override qua env `DOCKLITE_MAX_BODY_BYTES`.

---

## Dependencies at Risk

### WEAK PRACTICE — `github.com/docker/docker v26.1.5+incompatible`

- Files: `wsl-docker-service/go.mod` dòng 6, `wsl-docker-service/go.sum` dòng 15–16.
- Risk:
  - Suffix `+incompatible` → module chưa migrate sang go module hiện đại; downstream vẫn dùng GOPATH-style.
  - Docker v26.x có lịch sử CVE (CVE-2024-41110 authz plugin bypass, đã fix ở 26.1.5 — version hiện tại là fix). Cần giữ cập nhật khi 27.x/28.x ra security patch.
- Migration plan: Theo dõi [Moby release notes](https://github.com/moby/moby/releases); nâng lên 27.x sau khi kiểm tra API compatibility.

### SUSPICIOUS — Nhiều package `// indirect` chưa pin chính xác

- Files: `wsl-docker-service/go.mod` dòng 11–35 (đặc biệt `go.opentelemetry.io/*` — stack OTEL lớn đi theo docker client).
- Risk: Mỗi package indirect là attack surface mới; OTEL thường không cần thiết cho use-case này.
- Migration plan: Chạy `go mod why github.com/X/Y` để xác minh; nếu không dùng, có thể replace client Docker bằng HTTP thô gọn hơn. Chạy `govulncheck ./...` thường xuyên trong CI.

### WEAK PRACTICE — Actions chưa pin SHA

- Files: `.github/workflows/ci.yml` dòng 12, 13, 26, 27, 44, 45; `.github/workflows/release.yml` dòng 17, 19, 44, 56.
- Risk: Tag bị move / maintainer compromised → code attacker chạy với quyền của runner.
- Migration plan: Dùng Dependabot `schedule: { interval: "weekly" }` + `package-ecosystem: "github-actions"`; pin SHA; xem mục "Supply-chain" ở CRITICAL.

---

## Missing Critical Features

### SUSPICIOUS — Không có audit log trên service

- Problem: Không log ai (Origin, Authorization header hash, IP) gọi endpoint nhạy cảm như `/api/system/prune`, `/api/compose/up`, `/api/images/load`. `LogRequests` (`logging.go` dòng 22–37) chỉ log method + path + req_id + ms.
- Blocks: Không có khả năng forensic sau sự cố.
- Fix approach: Thêm log source IP, User-Agent (không log token), và phân biệt authenticated/unauthenticated.

### SUSPICIOUS — Không có cơ chế xoay token (rotation)

- Problem: Token 64-hex được đặt trong env WSL và trong `settings.json` WPF. Người dùng phải sửa đồng thời ở hai nơi → thường bị skip.
- Fix approach: Thêm endpoint `POST /api/auth/rotate` (chỉ chấp nhận khi có token cũ + chain request đúng); tự động cập nhật WPF.

---

## Test Coverage Gaps

### SUSPICIOUS — Không test cho shell spawn / wsl interop

- What's not tested:
  - `SpawnWslLifecycleScript` (shell injection risk).
  - `BuildLinuxSyncBashScript` (chưa đọc nội dung nhưng dựa tên).
  - `TryRestartServiceManually` + health polling loop.
- Files: `src/DockLite.Infrastructure/Wsl/WslDockerServiceAutoStart.cs` (1017 dòng, test coverage gần 0).
- Risk: Các bug bảo mật ở phần này không có regression test — dễ tái xuất hiện sau refactor.
- Priority: High.

### WEAK PRACTICE — Không test cho handler compose exec / trivy scan

- What's not tested: `composeServiceExec`, `ImageTrivyScan` (argument injection path).
- Files: `wsl-docker-service/internal/compose/compose_services.go`, `wsl-docker-service/internal/docker/image_trivy_scan.go`.
- Risk: Fix argument injection (đã đề xuất) mà không có test sẽ regress.
- Priority: High.

### WEAK PRACTICE — Chỉ có test `TestAPIHealth`/`TestAPIOpenAPIJSON`/`TestAPIDockerInfoWithEngine` ở integration

- Files: `wsl-docker-service/integration/api_integration_test.go`.
- Risk: Integration coverage gần như zero; toàn bộ lifecycle container/image/compose chỉ test cục bộ bằng smoke test.
- Priority: Medium.

---

## Checklist khắc phục ưu tiên

1. Đổi bind mặc định sang `127.0.0.1:17890`; fail-closed nếu addr != loopback và token trống (`wsl-docker-service/cmd/server/main.go`).
2. Escape `wslUnixPath` bằng `BashSingleQuoted` ở `SpawnWslLifecycleScript` (`WslDockerServiceAutoStart.cs` dòng 905).
3. DPAPI encrypt `ServiceApiToken` trước khi ghi `settings.json` (`AppSettingsStore.cs`).
4. Pin mọi GitHub Action bằng SHA; thay `curl | sh` syft bằng action pinned SHA hoặc binary pinned + verify checksum (`.github/workflows/release.yml`).
5. Siết `CheckOrigin` WebSocket mặc định loopback (`wslimit.go`).
6. Reject leading `-` ở `imageRef`, service name, profile name; thêm `--` separator trước user input khi gọi `docker compose` / `trivy`.
7. Chmod 0o600 + re-validate trước exec cho `~/.docklite/compose_projects.json`.
8. Thêm rate-limit + audit log (IP, req_id) cho REST/WebSocket.
9. Bỏ `docs/*.md` khỏi `.gitignore`.
10. Viết unit test cho `SpawnWslLifecycleScript`, `ImageTrivyScan` argument path, `composeServiceExec` argument path.

---

*Concerns audit: 2026-04-23*
