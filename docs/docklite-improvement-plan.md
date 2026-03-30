## Tiến độ thực hiện (cập nhật theo repo)

Tóm tắt nhanh: **mức 1–2 (một phần)** đã có: script WSL, log WSL, `LogsViewModel`, dialog service, `**AddDockLiteUi` + `IAppShellFactory`**, `MainWindow` gọn (chỉ `ShellViewModel` + dịch vụ khởi động), `**IAppStartupService**`, `**IAppShutdownToken**` (mở rộng sang nhiều ViewModel gọi API dài), nhật ký ứng dụng **lọc category/mức / sao chép chẩn đoán / xuất**, toast **nền** (`ShowAsync` + mức), **envelope JSON + `ApiResult<T>`**, Go **HTTP status + validate compose**, **Chọn thư mục** trên Compose; service Go **Engine API** cho container/image/prune/logs (mục 10), **compose** vẫn CLI (compose exec/plugin). **Hướng mở rộng**: inspect, batch, notifications, app log viewer, **top RAM / top CPU**, blkio trong snapshot stats, **compose exec -T**, **settings (distro + tóm tắt đường dẫn)** đã bổ sung; stats qua WebSocket chuyên biệt và shell `-it` tương tác vẫn **một phần** (xem bảng). Chi tiết theo từng mục nằm ở **Checklist triển khai** bên dưới. Nhật ký thay đổi: `CHANGELOG.md` mục «Chưa phát hành».

---

## Checklist triển khai

Trạng thái: **đã** = đã có trong repo; **một phần** = đã làm nhưng còn việc hoặc chưa đủ mục tiêu ban đầu; **chưa** = chưa bắt đầu hoặc chỉ nằm trong kế hoạch.

### Theo mục «Các vấn đề nên sửa trước» (mục 1–10)


| Mục | Nội dung                                                       | Trạng thái                                                                                                                                                                                                                                                                                                                 |
| --- | -------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | `MainWindow` / wiring khởi động                                | **Đã**: DI gom trong `AddDockLiteUi`; `IAppShellFactory`; `MainWindow` chỉ `ShellViewModel` + `IAppStartupService` + `IAppShutdownToken`; `AppHostContext` (base dir); `IAppStartupService.RunInitialLoadAsync` không tham số composition (inject `ShellCompositionResult` + `AppHostContext` vào `AppStartupCoordinator`) |
| 2   | Settings load một lần                                          | **Đã**: `AppShellFactory` load một lần; `SettingsViewModel` nhận `AppSettings` ban đầu                                                                                                                                                                                                                                     |
| 3   | Tách `build-server.sh` / `run-server.sh` / `restart-server.sh` | **Đã**                                                                                                                                                                                                                                                                                                                     |
| 4   | Thu stdout/stderr WSL + gợi ý khi health fail                  | **Đã**: buffer dòng gần nhất + lệnh/distro/path; `FormatHealthTimeoutUserHint`; log đầy đủ khi timeout; toast cuộn + thời gian hiển thị theo độ dài; Cài đặt (chờ health 90s) nối cùng gợi ý                                                                                                                               |
| 5   | `LogsViewModel` tránh lag                                      | **Một phần**: timer batch, `BeginInvoke`, incremental khi không search; `ListBox` ảo hóa + dòng log không wrap để virtualization ổn định hơn                                                                                                                                                                               |
| 6   | `IDialogService` thay `MessageBox`                             | **Đã** (Container, Image, Cleanup)                                                                                                                                                                                                                                                                                         |
| 7   | API client xử lý HTTP nhất quán                                | **Đã**: envelope + `ApiResult<T>` + `code`/`message`/`details`; `GetHealthAsync` không envelope                                                                                                                                                                                                                            |
| 8   | Go: HTTP status phản ánh lỗi (không chỉ 200 + `ok:false`)      | **Đã** (thân JSON envelope; không còn `ok`/`error` phẳng trên `/api/`* trừ health)                                                                                                                                                                                                                                         |
| 9   | Compose: validate path / file / duplicate + Browse folder      | **Đã** (Go: thư mục, file compose, duplicate; WPF: `OpenFolderDialog`)                                                                                                                                                                                                                                                     |
| 10  | Go: giảm spawn CLI / SDK                                       | **Một phần / đủ mục tiêu giai đoạn**: `internal/dockerengine` + Docker Engine API cho list/start/stop/restart/rm container, list/rmi/prune image, docker info, system prune, log HTTP + WebSocket; **vẫn** `exec docker compose …` trong `internal/compose` (Compose Plugin).                                                                                                                                 |


### Theo mức ưu tiên (Phương án sửa lỗi)

**Mức 1**


| Hạng mục                         | Trạng thái                                       |
| -------------------------------- | ------------------------------------------------ |
| Tách `run-server.sh` / build-run | **Đã**                                           |
| Bắt stderr/stdout WSL (log file) | **Đã** (xem mục 4: UI chi tiết chưa)             |
| Tối ưu `LogsViewModel`           | **Một phần** (xem mục 5)                         |
| Chuẩn hóa lỗi HTTP client + Go   | **Đã** (envelope + mã domain; health tách riêng) |
| Bỏ `MessageBox` khỏi ViewModel   | **Đã**                                           |


**Mức 2**


| Hạng mục                                 | Trạng thái                                                                                                                                                                    |
| ---------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Đưa tạo dependency ra khỏi `MainWindow`  | **Đã** (DI resolve `MainWindow`; `AppShellFactory` gọi trong `AddSingleton`)                                                                                                  |
| `AppStartupCoordinator`                  | **Đã**                                                                                                                                                                        |
| Validation compose project               | **Đã** (phía Go; có thêm nút chọn thư mục Windows)                                                                                                                            |
| `CancellationToken` cho command chạy lâu | **Một phần**: `IAppShutdownToken` truyền vào gọi API dài ở `ContainersViewModel`, `ComposeViewModel`, `ImagesViewModel`, `CleanupViewModel`, `LogsViewModel` (kể WebSocket follow); `AppStartupCoordinator` và `SettingsViewModel` giữ như trước; còn VM khác nếu thêm lệnh chạy lâu |
| Retry/backoff health check               | **Một phần**: retry ngắn (3 lần, 250 ms) **trước** khi spawn WSL; vòng chờ sau spawn vẫn 500 ms + một GET/health mỗi lần                                                      |


**Mức 3**


| Hạng mục                                                              | Trạng thái                                                                             |
| --------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| `ApiEnvelope<T>`                                                      | **Đã** (Contracts + Go `internal/apiresponse`)                                         |
| `INotificationService`, `IAppStartupService`                          | **Đã** (`WpfToastNotificationService`, `AppStartupCoordinator` : `IAppStartupService`) |
| Chia package Go (`internal/httpserver`, `docker`, `compose`, `ws`, …) | **Đã** (xem mục «Mức 3» trong phương án)                                               |


### Hướng mở rộng (tính năng)


| Hạng mục                                                        | Trạng thái                                                                                                                                                                                                                                                                    |
| --------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Container inspect / detail                                      | **Đã** (API inspect + Expander «Chi tiết» / nút tải; mounts/env/network nằm trong JSON, chưa tách cột riêng)                                                                                                                                                                    |
| Stats realtime                                                  | **Một phần**: snapshot + polling; **Top 5 RAM** (`/api/containers/top-by-memory`) và **Top 5 CPU** (`/api/containers/top-by-cpu`); snapshot có tổng block read/write (blkio); chưa stream WebSocket stats riêng cho stats |
| Compose: service-level (list service, logs/shell theo service)  | **Một phần**: `config --services`, start/stop/logs; **exec không TTY** (`docker compose exec -T`, lệnh từ ô trong UI); **shell tương tác** (`-it`) vẫn cần terminal ngoài                                                                                                                                                          |
| Batch actions (containers/images)                               | **Đã** (ô chọn, batch start/stop/xóa container; chọn nhiều image xóa)                                                                                                                                                                                                          |
| Settings UX (test distro, browse folder, effective WSL path, …) | **Đã** (phần còn lại nhỏ): **Thử distro** (`uname -a` qua `wsl.exe`); **Cập nhật tóm tắt** đường dẫn (distro + thư mục service Windows→WSL + base URL); **Thử wslpath**; **Chọn thư mục** Compose; kiểm tra kết nối + Docker info                                                                                                        |
| Notifications / nền                                             | **Đã**: `ShowAsync` + mức Info/Warning/Success; tổng quan (chuyển trạng thái kết nối), Compose lỗi, prune image; health timeout lúc khởi động                                                                                                                                    |
| App log viewer: filter, export, copy diagnostics                | **Đã**: lọc category + mức (heuristic) + chuỗi; `ListBox` ảo hóa; xuất (có lọc → phần đang xem / không → đuôi đầy đủ); sao chép chẩn đoán kèm phiên bản, OS, bộ lọc |


---

## Các vấn đề nên sửa trước

Các mục dưới đây giữ nguyên như bối cảnh và hướng sửa; **trạng thái đã làm / chưa làm** của repo hiện tại nằm ở mục **Checklist triển khai** (ví dụ `MainWindow` đã gọi `AppShellFactory` thay vì tự new từng dependency như mô tả ban đầu).

### 1 `MainWindow` đang ôm quá nhiều trách nhiệm

Hiện `MainWindow.xaml.cs` tự tạo `AppSettingsStore`, `DockLiteHttpSession`, API clients, tất cả ViewModel, rồi còn tự gọi auto-start và refresh dashboard trong event `Loaded`. Cách này chạy được, nhưng sẽ khó test, khó thay đổi dependency, và càng về sau càng phình code-behind.

**Nên sửa**

- Tạo một `Bootstrapper` hoặc dùng `Microsoft.Extensions.DependencyInjection`.
- `App.xaml.cs` khởi tạo DI container.
- `MainWindow` chỉ nhận `ShellViewModel` qua constructor.
- Luồng khởi động app nên nằm trong `AppStartupCoordinator` riêng.

### 2 `Settings` đang bị load hai lần

`MainWindow` load settings một lần, rồi `SettingsViewModel` lại tự load lại từ store thêm lần nữa. Điều này không hỏng ngay, nhưng dễ gây lệch state nếu sau này có startup override hoặc migrate config.

**Nên sửa**

- `MainWindow` hoặc DI container load `AppSettings` đúng một lần.
- Inject `AppSettingsSnapshot` hiện tại vào `SettingsViewModel`.

### 3 `run-server.sh` đang làm quá nhiều việc mỗi lần start

Script hiện tại chạy `go mod tidy`, `go build`, rồi `exec ./bin/docklite-wsl` mỗi lần khởi động service. Điều này khiến startup chậm, làm thay đổi `go.mod/go.sum` ngoài ý muốn, và biến thao tác “start service” thành “build lại cả service”

**Nên sửa**  
Tách thành 3 script:

- `build-server.sh`: chỉ build
- `run-server.sh`: chỉ chạy binary
- `restart-server.sh`: stop + run

Mẫu logic tốt hơn:

- `run-server.sh` kiểm tra binary có tồn tại không
- nếu không có thì báo lỗi rõ hoặc gọi build riêng
- nếu có thì chạy luôn, không `go mod tidy` mỗi lần

### 4 Auto-start WSL chưa thu được stdout/stderr

`WslDockerServiceAutoStart.StartWslRunServer` chỉ `Process.Start(psi)` rồi trả về. Khi service fail do `go`, `docker`, `PATH`, `permission`, app gần như không thu được log gốc để hiển thị. Điều này làm việc debug rất khó.
**Nên sửa**

- Có mode manual start dùng `RedirectStandardOutput/RedirectStandardError`.
- Ghi log ra file riêng.
- Khi health fail sau timeout, hiển thị:
  - lệnh đã chạy
  - distro
  - path WSL
  - stderr/stdout gần nhất

### 5 `LogsViewModel` có nguy cơ lag UI

Hiện log stream gọi `Dispatcher.Invoke` cho từng chunk, rồi `AppendStreamChunk()` lại có thể `ApplySearchFilter()` bằng cách clear và add lại toàn bộ `ObservableCollection`. Với log nhiều dòng, UI sẽ lag khá rõ.

**Nên sửa**

- Đổi sang `Dispatcher.BeginInvoke`.
- Batch append theo timer 100–200ms.
- Chỉ filter incremental nếu đang search.
- Không rebuild toàn bộ `Lines` mỗi chunk.
- Cân nhắc virtualized log viewer.

### 6 ViewModel đang dính UI framework

`ContainersViewModel` đang gọi `System.Windows.MessageBox.Show(...)` trực tiếp. Cái này làm ViewModel khó test và phá MVVM khá rõ.

**Nên sửa**

- Tạo `IDialogService`
- VM chỉ gọi `await _dialogService.ConfirmAsync(...)`
- phần hiển thị thật nằm ở layer UI

### 7 API client xử lý status code chưa nhất quán

Một số API có `EnsureSuccessStatusCode()`, nhưng nhiều chỗ chỉ deserialize response luôn. Ví dụ `GetContainerLogsAsync`, `AddComposeProjectAsync`, `RemoveComposeProjectAsync`, `RemoveImageAsync`, `PruneImagesAsync`, `SystemPruneAsync` đều chưa có chuẩn chung về xử lý lỗi HTTP

**Nên sửa**

- Chuẩn hóa mọi response:
  - HTTP status đúng
  - body dạng `ApiEnvelope<T>`
- Ở client:
  - luôn check `response.IsSuccessStatusCode`
  - parse lỗi JSON nếu có
  - map về exception/domain error thống nhất

### 8 Go service đang trả lỗi business bằng HTTP 200 quá nhiều

Trong nhiều handler, lỗi vẫn trả JSON `{ok:false}` nhưng status code không phản ánh lỗi thật. Ví dụ list containers, remove container, compose commands. Điều này làm client khó phân biệt lỗi app với lỗi business.

**Nên sửa**

- `400`: request sai
- `404`: không tìm thấy container/project
- `409`: conflict
- `500`: lỗi server/docker

### 9 Compose add chưa validate kỹ

Hiện `compose.go` chỉ convert path rồi lưu project, chưa thấy check:

- thư mục có tồn tại không
- có `docker-compose.yml` / `compose.yml` không
- project đã tồn tại chưa
- path trùng chưa

**Nên sửa**

- Validate path
- Detect file compose
- Chặn duplicate theo normalized WSL path
- Có nút “Browse folder” ở WPF thay vì người dùng gõ tay

### 10 Service Go: giảm spawn CLI / dùng Engine API

**Trạng thái hiện tại**

- Đã chuyển sang `github.com/docker/docker/client` qua `internal/dockerengine` (singleton, `FromEnv`, đàm phán phiên API): container (list, start/stop/restart, rm, logs), image (list, remove, prune), `Info`, prune (containers/images/networks/volumes/system ghép API), WebSocket log (`ContainerLogs` + `stdcopy`).
- Chỉ còn spawn CLI cho **Compose**: `internal/compose` gọi `docker compose …` (`up`/`down`/`ps`), vì Compose V2 tích hợp plugin; phần còn lại không còn `exec docker` cho các route trên.

**Có thể làm thêm (không chặn)**

- Stats realtime (`/containers/{id}/stats` hoặc stream) vẫn chưa có trong app; khi cần nên dùng API `ContainerStats` thay vì CLI.
- Nếu sau này bỏ hẳn plugin Compose CLI: xem Compose-spec qua thư viện hoặc API tách (phụ thuộc roadmap Docker).

## Phương án sửa lỗi theo mức ưu tiên

### Mức 1: nên làm ngay

- Tách `run-server.sh` thành build/run riêng
- Sửa startup để bắt stderr/stdout của WSL service
- Tối ưu `LogsViewModel` để tránh lag
- Chuẩn hóa lỗi HTTP ở API client và Go service
- Bỏ `MessageBox` ra khỏi ViewModel

### Mức 2: nên làm trước khi thêm nhiều feature

- Đưa dependency creation ra khỏi `MainWindow`
- Gom startup flow thành `AppStartupCoordinator`
- Thêm validation cho compose project
- Thêm `CancellationToken` cho các command chạy lâu
- Thêm retry/backoff rõ ràng cho health check

### Mức 3: refactor nền

- Tạo `ApiEnvelope<T>` (đã)
- `IDialogService`, `INotificationService`, `IAppStartupService` (đã trong WPF)
- Chia Go service thành (đã làm trong repo, tên gói tránh trùng `net/http`):
  - `internal/httpserver` — `Register`, `LogRequests`, `ReadHeaderTimeout`
  - `internal/docker` — health, docker info, containers, images, system prune (handlers gọi Engine API qua `dockerengine`)
  - `internal/dockerengine` — client Docker API, ánh xạ `errdefs` → HTTP (`WriteError`)
  - `internal/compose` — lưu project + route compose (CLI `docker compose` duy nhất)
  - `internal/ws` — WebSocket log container
  - `internal/settings` — placeholder (import trống trong `main`, mở rộng sau)
- `cmd/server/main.go` chỉ còn cấu hình địa chỉ, `ServeMux`, `ListenAndServe`.

## Hướng mở rộng hợp lý

### 1 Container details / inspect

Hiện app mạnh ở list/action, nhưng thiếu màn detail.  
Nên thêm:

- inspect JSON
- mounts
- env vars
- restart policy
- network list

**Tiến độ:** Service Go `GET /api/containers/{id}/inspect` trả `data.inspect` (JSON thô từ Engine). Trang **Container** (WPF) có Expander «Chi tiết», nút **Tải chi tiết** để tải inspect (và stats, xem mục 2). Mounts/env/restart/network nằm trong JSON inspect; chưa tách cột riêng trong UI.

### 2 Stats realtime

Đây là feature người dùng Docker GUI rất hay cần:

- CPU
- memory
- network I/O
- block I/O
- top 5 container theo RAM/CPU

**Tiến độ:** `GET /api/containers/{id}/stats` trả một snapshot (CPU % ước lượng từ `cpu_stats`/`precpu_stats`, RAM usage/limit, tổng RX/TX mạng). Trang **Container**: checkbox **Stats realtime (polling API)** + chọn chu kỳ (1–10 giây) gọi lại endpoint theo `DispatcherTimer` khi có container được chọn; tắt realtime thì gọi thêm một lần để bỏ dòng gợi ý realtime. Nút **Top 5 RAM (snapshot)** gọi `GET /api/containers/top-by-memory?limit=5` (song song stats trên server, sắp theo RAM). Chưa có WebSocket stream từ daemon, chưa block I/O tách dòng.

### 3 Compose service details

Ngoài `up/down/ps`, nên có:

- list service trong project
- start/stop service lẻ
- logs theo service
- open shell theo service

**Tiến độ:** Go: `POST /api/compose/config/services` (`docker compose config --services`), `POST /api/compose/service/start|stop`, `POST /api/compose/service/logs` (tail 1–10000), `POST /api/compose/service/exec` (`docker compose exec -T`, lệnh tách bằng khoảng trắng, chặn ký tự nguy hiểm). Trang **Compose**: ListBox service, nút tải danh sách / start / stop / logs / **Chạy exec**. **Shell tương tác (-it)** vẫn cần terminal bên ngoài.

### 4 Batch actions

Cho containers/images:

- start selected
- stop selected
- remove selected
- prune theo loại

**Tiến độ:** Trang **Container**: cột ô chọn, **Chọn tất cả** / **Bỏ chọn**, **Start đã chọn** / **Stop đã chọn** / **Xóa đã chọn** (xác nhận theo số lượng). Trang **Image**: chọn nhiều dòng, **Xóa các dòng đã chọn**; prune dangling / `-a` vẫn như cũ.

### 5 Better UX cho settings

Hiện settings đủ dùng, nhưng nên thêm:

- test distro
- test `docker info`
- test `wslpath`
- nút chọn folder
- hiển thị “effective WSL path”

**Tiến độ:** **Kiểm tra kết nối** hiển thị thêm phiên bản Engine và OS khi đọc được Docker info. **Thử wslpath**: ô đường dẫn Windows + nút **Chạy wslpath**. **Thử distro**: chạy `uname -a` trong distro đã nhập (hoặc distro mặc định). **Cập nhật tóm tắt**: hiển thị distro, thư mục service Windows→WSL (nếu có), base URL. Chưa có nút “test distro” kiểu đặc biệt khác ngoài `uname`.

### 6 Notifications và trạng thái nền

Rất hữu ích:

- service down
- docker unreachable
- compose command fail
- image prune complete

**Tiến độ:** `INotificationService.ShowAsync` + `NotificationDisplayKind` (Info / Warning / Success) trong `WpfToastNotificationService`. **Tổng quan:** toast khi chuyển từ kết nối ổn sang mất (service HTTP không phản hồi hoặc Docker lỗi), và toast xanh khi từ lỗi trở lại ổn. **Compose:** lệnh compose / service thất bại hoặc exception có toast cảnh báo. **Image prune:** thành công toast xanh, thất bại toast cảnh báo. Health timeout lúc khởi động vẫn dùng toast cảnh báo.

### 7 App log viewer tốt hơn

Bạn đã có `AppDebugLogViewModel` trong shell. Hướng tốt là biến nó thành màn chẩn đoán thật sự:

- filter theo category
- level
- export log
- “copy diagnostics” 1 click

**Tiến độ:** ComboBox **category** (từ cột tab trong file log) và **mức** (Lỗi / Cảnh báo / Thông tin — suy đoán từ từ khóa trong dòng); lọc chuỗi giữ nguyên. Danh sách dòng hiển thị bằng `ListBox` ảo hóa. **Xuất file:** nếu đang bật bất kỳ bộ lọc nào thì ghi phần đang xem; không thì ghi toàn bộ đuôi đã đọc. **Sao chép chẩn đoán:** thêm phiên bản assembly, OS, và trạng thái bộ lọc.

