# DockLite

Ứng dụng desktop Windows (WPF) và service Go trong WSL để quản lý Docker Engine. Tài liệu kiến trúc: `docs/docker_gui_wpf_go_wsl_analysis.md`, lộ trình: `docs/docker_gui_roadmap.md`.

## Yêu cầu

- Windows 10/11, .NET SDK 8 (hoặc tương thích để build `net8.0-windows`).
- WSL2 (Ubuntu khuyến nghị) và Go 1.22+ khi build service.

## Build ứng dụng WPF

```powershell
dotnet build DockLite.slnx -c Release
```

Chạy thử:

```powershell
dotnet run --project src/DockLite.App/DockLite.App.csproj
```

Nếu `dotnet sln` tạo file `DockLite.sln` (định dạng cũ), dùng tên file tương ứng trên máy bạn.

## Build service Go (trong WSL)

Mặc định service lắng nghe `127.0.0.1:17890`. Một số endpoint:

- `GET /api/metrics` (text/plain; bộ đếm request HTTP đơn giản, phục vụ quan sát nhẹ)
- `GET /api/health`
- `GET /api/docker/info` (gọi `docker info` trong WSL)
- `GET /api/containers` (gọi `docker ps -a`)
- `POST /api/containers/stats-batch` body JSON `{ "ids": ["..."] }` (tối đa 32 id) — nhiều snapshot stats trong một yêu cầu; mỗi phần tử `items` có `ok`, `stats` hoặc `error`
- `POST /api/containers/{id}/start|stop|restart`
- `DELETE /api/containers/{id}?force=true|false`
- `GET /api/containers/{id}/logs?tail=` (nội dung log gần nhất, JSON `{ "content": "..." }`)
- WebSocket `GET /ws/containers/{id}/logs` (stream `docker logs -f`, cần module `github.com/gorilla/websocket`)
- WebSocket `GET /ws/containers/{id}/stats?intervalMs=` (stream `docker stats`, mỗi tin nhắn văn bản một JSON cùng schema `GET /api/containers/{id}/stats`; `intervalMs` 500–5000, mặc định 1000 — giới hạn tần suất gửi)
- Compose (project lưu trong `~/.docklite/compose_projects.json` trên WSL):
  - `GET /api/compose/projects`
  - `POST /api/compose/projects` body `{ "windowsPath": "C:\\\\..." }` hoặc `{ "wslPath": "/mnt/..." }`
  - `DELETE /api/compose/projects/{id}`
  - `POST /api/compose/up` | `down` | `ps` body `{ "id": "..." }`
- Image:
  - `GET /api/images` (gọi `docker images --format '{{json .}}'`)
  - `POST /api/images/remove` body `{ "id": "..." }` (tương đương `docker rmi`)
  - `POST /api/images/prune` body `{ "allUnused": true|false }` (`docker image prune -f` hoặc `-a -f`)
  - `GET /api/images/{id}/inspect` | `GET /api/images/{id}/history` | `GET /api/images/{id}/export` (tar, không dùng envelope JSON)
  - `POST /api/images/pull` body `{ "reference": "nginx:latest" }` (log rút gọn trong envelope)
  - `POST /api/images/load` thân `application/x-tar` (tối đa 512 MiB trên server)
- Mạng và volume (chỉ đọc):
  - `GET /api/networks` | `GET /api/volumes`
- Dọn hệ thống:
  - `POST /api/system/prune` body `{ "kind": "containers"|"images"|"volumes"|"networks"|"system", "withVolumes": true|false }` (với `kind: system`, `withVolumes` bật `--volumes`)

Cần Docker Engine, `docker` và `docker compose` trong PATH của WSL.

```bash
cd wsl-docker-service
bash scripts/build-server.sh   # lần đầu hoặc sau khi đổi mã (go mod tidy + go build)
bash scripts/run-server.sh     # chỉ chạy binary; thiếu binary thì báo lỗi và thoát
# Hoặc: ./bin/docklite-wsl
```

Nếu bạn sửa mã trên Windows và muốn có cùng nội dung trong filesystem Linux của WSL (ví dụ `/home/.../wsl-docker-service` để build nhanh hơn so với chỉ `/mnt/c/...`): trong **Cài đặt**, tab **WSL và service**, điền **Nguồn trong Windows** (clone trên ổ C:; để trống nếu trùng với ô thư mục dịch vụ), **Đích trong WSL (đường dẫn Unix)**, rồi nhấn **Đồng bộ mã nguồn vào WSL** (DockLite gọi `rsync` hoặc `cp` trong distro). Nên cài `rsync` trong distro nếu muốn đồng bộ có xóa file thừa ở đích.

Trong `wsl-docker-service/internal/appversion/` có file **VERSION** (một dòng, ví dụ `0.1.0`) — service Go embed file này cho trường `version` trong `/api/health`. Tùy chọn **Chỉ đồng bộ khi version nguồn >= đích** tránh ghi đè bản đã copy lên WSL mới hơn bằng bản Windows cũ; lần đầu (chưa có `VERSION` trên đích) coi như `0.0.0`.

Biến môi trường tùy chọn: `DOCKLITE_ADDR` (ví dụ `127.0.0.1:17890`).

### TLS và truy cập ngoài máy cục bộ

Service Go phục vụ qua **HTTP** (không TLS trong binary). Dùng `127.0.0.1` / WSL trên cùng máy là mô hình phổ biến. Nếu cần truy cập từ máy khác trong LAN: **không** nên mở HTTP thông qua firewall mà không có lớp bảo vệ; nên đặt **reverse proxy** (nginx, Caddy, Traefik, …) với **HTTPS**, chứng chỉ phù hợp, và giới hạn nguồn truy cập. DockLite không nhúng TLS trực tiếp vào `docklite-wsl`.

## Cài đặt ứng dụng

Cấu hình lưu trong `%LocalAppData%\DockLite\settings.json` (ứng dụng đọc khi khởi động). Trên màn **Cài đặt** bạn có thể sửa rồi **Lưu**: `ServiceBaseUrl` (địa chỉ HTTP tới service Go trong WSL), `HttpTimeoutSeconds` (30–600, mặc định 120), đường dẫn tới thư mục `wsl-docker-service`, tên distro WSL, và có bật **tự khởi động service WSL khi mở app** hay không.

### Vị trí file (tóm tắt)

1. `DockLite.App.exe` chạy trên Windows (ví dụ `C:\Users\...\Desktop\DockLite.App.exe` hoặc thư mục publish).
2. Mã nguồn service Go nằm trong thư mục **`wsl-docker-service`**. Thư mục đó có thể:
   - nằm **cùng cây thư mục** với exe (hoặc repo DockLite trên ổ Windows), hoặc
   - chỉ tồn tại **trong WSL** (ví dụ `/home/.../wsl-docker-service`); khi đó trên Windows bạn vẫn truy cập được qua đường dẫn dạng `\\wsl.localhost\<tên-distro>\home\...\wsl-docker-service` và điền đường dẫn đó vào Cài đặt.

### Luồng khi mở ứng dụng (tự khởi động)

Ứng dụng **luôn mở cửa sổ chính** (sidebar + trang Tổng quan); không có chế độ «chỉ hiện giao diện sau khi đã kết nối». Sau khi cửa sổ tải xong, code thực hiện lần lượt:

1. Gọi `GET /api/health` tới `ServiceBaseUrl` đã lưu (mặc định `http://127.0.0.1:17890/` hoặc giá trị bạn đã cấu hình).
2. Nếu **đã bật** «Tự khởi động service trong WSL» trong cài đặt (mặc định bật) **và** bước 1 lỗi: gửi lệnh tới `wsl.exe` chạy `bash scripts/restart-server.sh` trong thư mục `wsl-docker-service` đã xác định (dừng tiến trình cũ rồi chạy lại binary; xem phần dưới), rồi **chờ lặp** gọi health trong thời gian chờ đã cấu hình (tab **Chờ và health**, mặc định vài chục giây).
3. Nếu **tắt** tự khởi động: chỉ thử bước 1, không gọi WSL.
4. Sau đó trang **Tổng quan** tự làm mới: nếu vẫn không kết nối được service, **dòng trạng thái** trên Tổng quan (và các trang khác khi bạn dùng) hiển thị thông báo lỗi kết nối — **không** có hộp thoại chặn toàn bộ ứng dụng. Bạn có thể vào **Cài đặt** để sửa địa chỉ, bật nút khởi động service, v.v.

### Luồng trong màn Cài đặt

1. Bạn nhập **Địa chỉ base URL** (và tùy chọn **Điền IP WSL**, **Lưu** nếu muốn ghi vào file).
2. **Khởi động service WSL**: áp dụng tạm địa chỉ/timeout từ ô cho `HttpClient`, gửi lệnh WSL chạy `run-server.sh`, chờ `/api/health` tối đa khoảng **90 giây**; kết quả hiện trong ô trạng thái phía dưới (thành công hoặc hết thời gian / gợi ý).
3. **Kiểm tra kết nối**: chỉ gọi API health và Docker info — **không** tự khởi động WSL; dùng để xác nhận sau khi service đã chạy (tay hoặc qua nút trên).

### Tìm thư mục `wsl-docker-service` và tự khởi động

- **Tự tìm**: từ thư mục chứa exe, đi ngược lên tối đa 10 cấp để tìm thư mục tên `wsl-docker-service` (cần có `scripts/restart-server.sh` cho tự khởi động khi mở app, và `scripts/run-server.sh` cho nút **Khởi động service WSL** trong Cài đặt). Khi chạy từ repo hoặc khi bạn đặt exe cùng cây thư mục với clone, thường không cần cấu hình thêm.
- **Chỉ định tay**: trong màn Cài đặt, điền đường dẫn Windows tới thư mục `wsl-docker-service` nếu exe nằm xa repo (ví dụ chỉ copy file publish ra chỗ khác).
- **Mã service chỉ nằm trong WSL** (ví dụ `/home/doanquangnd/workspace/projects/wsl-docker-service`): tự động từ thư mục exe **sẽ không** tìm thấy thư mục đó. Hãy điền đường dẫn Windows trỏ vào cùng thư mục đó, ví dụ:
  - `\\wsl.localhost\Ubuntu-22.04\home\doanquangnd\workspace\projects\wsl-docker-service`
  - hoặc `\\wsl$\Ubuntu-22.04\home\doanquangnd\workspace\projects\wsl-docker-service`  
  (Dán nguyên trong Explorer hoặc ô Thư mục wsl-docker-service trong Cài đặt, rồi Lưu.) Trường **Distro WSL** có thể ghi `Ubuntu-22.04` nếu cần ép đúng distro; với đường dẫn UNC, ứng dụng cũng có thể suy ra tên distro từ đường dẫn.
- **Script từ Windows** khi không có `wsl-docker-service` trong repo DockLite trên ổ C:  
  `.\scripts\Start-DockLiteWsl.ps1 -WslUnixPath '/home/doanquangnd/workspace/projects/wsl-docker-service' -WslDistribution Ubuntu-22.04`  
  (tương tự `Build-GoInWsl.ps1`).
- **Distro WSL**: nếu có nhiều distro, **nên** ghi đúng tên distro chứa project (ví dụ `Ubuntu-22.04`). Để trống thì `wsl.exe` dùng distro **mặc định** — nếu mặc định không phải máy có mã `wsl-docker-service`, tự khởi động có thể chạy nhầm distro.
- **Tắt tự khởi động**: bỏ chọn ô trong Cài đặt và **Lưu**; khi mở app chỉ kiểm tra health, không gọi `wsl` để chạy script. Bạn tự khởi động service trong WSL hoặc bằng nút **Khởi động service WSL** khi cần.

**Gỡ lỗi «Không kết nối được tới service WSL» (Kiểm tra kết nối):**

1. `go build` **chưa** chạy server — trong **đúng** distro (ví dụ Ubuntu-22.04), tại thư mục project: `./bin/docklite-wsl` (hoặc `bash scripts/run-server.sh`). Giữ terminal mở; khi đó mới có process lắng nghe cổng (mặc định `0.0.0.0:17890` trong code, vẫn truy cập từ Windows qua `http://127.0.0.1:17890/`).
2. Điền **Distro WSL** = `Ubuntu-22.04` rồi **Lưu** nếu bạn dùng tự khởi động và distro mặc định không trùng máy chứa mã.
3. Sau khi sửa service Go, build lại binary trong WSL rồi chạy lại.
4. Trên **WSL2**, đôi khi Windows **không** chuyển tiếp `127.0.0.1` tới process trong WSL dù service đã lắng nghe. Trong Cài đặt DockLite, dùng nút **Điền IP WSL** (lấy IP bằng `wsl hostname -I`), rồi **Lưu**; hoặc trong PowerShell gõ `wsl hostname -I`, copy IPv4 đầu tiên và đặt `ServiceBaseUrl` thành `http://IP:17890/`. Có thể bật chế độ mạng mirrored trong `.wslconfig` (Windows 11, WSL gần đây) để `127.0.0.1` ổn định hơn.
5. Nút **Khởi động service WSL** gọi `bash -lc` (login shell) để `go` có trong PATH giống terminal tương tác; sau đó app chờ `/api/health` tối đa khoảng 90 giây. Nếu vẫn lỗi: `run-server.sh` **chỉ chạy binary** đã build (không gọi `go build`). Nếu thiếu binary hoặc cần build lại mã, trong WSL chạy `bash scripts/build-server.sh`, sau đó `bash scripts/run-server.sh` hoặc `./bin/docklite-wsl`. Lỗi `go mod` / `go build` chỉ xuất hiện khi dùng `build-server.sh` (hoặc build tay), không phải từ `run-server.sh` khi binary đã có.
6. Đường dẫn dạng `\\wsl.localhost\Ubuntu-22.04\...`: app **tự lấy tên distro** từ UNC (kể cả khi ô Distro WSL để trống) và gọi `wsl -d …` cho `wslpath` và `bash`, tránh nhầm **distro mặc định** của Windows khác máy chứa mã. Lỗi «actively refused» tới IP WSL thường là chưa có process lắng nghe hoặc IP WSL đổi sau `wsl --shutdown`: dùng lại **Điền IP WSL** rồi **Lưu**.

Cần WSL đã cài, `wsl.exe` trong `PATH`, và trong distro có Go (để `run-server.sh` build) cùng Docker như trước.

Các GET (health, danh sách, …) qua `DockLiteApiClient` vẫn tự thử lại tối đa 3 lần khi lỗi mạng tạm thời.

## Phát hành MVP (Tuần 8)

### Ứng dụng Windows (exe)

Từ thư mục gốc repo, PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\Publish-Wpf.ps1
```

Kết quả mặc định: `artifacts\publish-win-x64\DockLite.App.exe` (self-contained, win-x64, gộp một file). Không cần cài .NET Runtime trên máy đích.

Tùy chọn:

- `-FrameworkDependent`: bản nhỏ hơn, yêu cầu [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (win-x64).
- `-NoSingleFile`: không gộp một file (nhiều DLL, dễ gỡ lỗi).

Ví dụ:

```powershell
.\scripts\Publish-Wpf.ps1 -FrameworkDependent
.\scripts\Publish-Wpf.ps1 -OutputRelativePath "artifacts\publish-fdd"
```

### Service Go (WSL)

Build binary trong WSL từ Windows (cần WSL và Go trong distro):

```powershell
.\scripts\Build-GoInWsl.ps1
```

Binary: `wsl-docker-service/bin/docklite-wsl` (trong filesystem WSL).

Chạy service (build + lắng nghe `127.0.0.1:17890`):

```powershell
.\scripts\Start-DockLiteWsl.ps1
```

Chỉ định distro (khi có nhiều bản WSL):

```powershell
.\scripts\Start-DockLiteWsl.ps1 -WslDistribution Ubuntu
```

Hoặc trong WSL trực tiếp (ưu tiên `bash` để tránh `Permission denied` nếu file `.sh` không có bit thực thi):

```bash
cd wsl-docker-service
bash scripts/run-server.sh
```

### Phiên bản

Phiên bản assembly/file được đặt trong `Directory.Build.props` (hiện tại `0.1.0`).

## Kiểm tra nhanh

1. Chạy binary Go trong WSL (Docker đã cài trong WSL).
2. Chạy ứng dụng WPF trên Windows.
3. Màn Tổng quan: trạng thái service và thông tin Docker Engine nếu cổng được forward tới WSL.
