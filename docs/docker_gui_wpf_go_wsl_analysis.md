# Phân tích yêu cầu và kế hoạch phát triển ứng dụng quản lý Docker GUI nhẹ

Phần tổng quan và mô tả ứng dụng nằm trong `README.md` ở thư mục gốc repo. Tài liệu này tiếp tục với mục tiêu chức năng, kiến trúc, API, rủi ro và kế hoạch phát triển.

---

## 1. Mục tiêu chức năng

### 1.1. Mục tiêu chính
Ứng dụng cần giải quyết tốt các nhu cầu thường xuyên nhất:

- Xem danh sách container.
- Start / Stop / Restart / Remove container.
- Xem log dễ đọc.
- Theo dõi trạng thái project docker compose.
- Xem images đang có.
- Dọn dẹp tài nguyên không dùng.
- Xem nhanh CPU, memory, network của container.
- Quản lý nhiều project compose theo danh sách yêu thích.

### 1.2. Mục tiêu phi chức năng
- Phản hồi nhanh.
- Khởi động nhẹ.
- Giao diện rõ ràng, ít rối.
- Dễ bảo trì.
- Dễ mở rộng thêm công cụ khác trong tương lai.
- Không phụ thuộc Docker Desktop.
- Chỉ giao tiếp local, không mở ra internet.

---

## 2. Phạm vi ứng dụng

### 2.1. Trong phạm vi
- Quản lý Docker Engine trong WSL2.
- Quản lý container, images, logs, stats.
- Hỗ trợ Docker Compose ở mức thao tác phổ biến.
- Cấu hình các project compose yêu thích.
- Giao tiếp local giữa WPF và service trong WSL.

### 2.2. Ngoài phạm vi giai đoạn đầu
- Không thay thế hoàn toàn Docker Desktop.
- Không quản lý Kubernetes.
- Không quản lý Swarm.
- Không multi-host Docker.
- Không remote Docker daemon qua internet.
- Không quản lý registry nâng cao ở giai đoạn đầu.
- Không chỉnh sửa Dockerfile/compose file trực tiếp trong app ở giai đoạn đầu.

---

## 3. Kiến trúc hệ thống

### 3.1. Kiến trúc tổng thể

```text
+---------------------------+
| Windows WPF Application   |
| (WPF + MVVM)              |
+------------+--------------+
             |
             | HTTP / WebSocket
             v
+---------------------------+
| WSL Docker Service        |
| (Go)                      |
+------------+--------------+
             |
             | Docker SDK / CLI
             v
+---------------------------+
| Docker Engine in WSL2     |
+---------------------------+
```

### 3.2. Lý do chọn kiến trúc này
- WPF phù hợp xây dựng ứng dụng desktop Windows có UI tốt.
- MVVM giúp code UI sạch, dễ test và dễ mở rộng.
- Go phù hợp để làm service nhỏ, nhẹ, hiệu suất cao.
- Docker interaction nằm trong WSL nên logic kỹ thuật được gom về đúng nơi.
- WPF chỉ cần gọi API, tránh phải tự parse CLI output phức tạp.

---

## 4. Lý do chọn công nghệ

### 4.1. WPF
Ưu điểm:
- Native desktop app cho Windows.
- Hỗ trợ binding mạnh.
- Phù hợp với MVVM.
- Dễ xây UI nhiều màn hình.
- Hỗ trợ DataGrid, command, styles, templates tốt.
- Có thể phát triển giao diện chuyên nghiệp hơn WinForms.

### 4.2. MVVM
Ưu điểm:
- Tách View và business logic.
- Dễ maintain.
- Dễ viết module.
- Dễ tái sử dụng component.
- Dễ unit test phần logic.

### 4.3. Go cho service trong WSL
Ưu điểm:
- Binary nhỏ, nhẹ.
- Hiệu suất tốt.
- Concurrency tốt, phù hợp cho stream logs/stats.
- Khởi động nhanh.
- Dễ đóng gói và deploy.
- Phù hợp làm local service.

### 4.4. Docker SDK + CLI
Chiến lược đề xuất:
- Dùng **Docker SDK** cho:
  - containers
  - images
  - stats
  - inspect
  - logs stream
- Dùng **docker compose CLI** cho:
  - up
  - down
  - restart
  - ps
  - logs theo project

Lý do:
- Docker SDK cho dữ liệu chuẩn và mạnh.
- Compose CLI thực tế và ổn định hơn cho thao tác compose project.

---

## 5. Mô hình giao tiếp

### 5.1. Giao thức
- HTTP REST cho request/response thông thường.
- WebSocket cho stream logs và stats realtime.

### 5.2. Luồng giao tiếp
- WPF gửi request đến service trong WSL.
- Service xử lý và trả JSON.
- Với logs/stats realtime, service stream qua WebSocket.

### 5.3. Bảo mật
Do ứng dụng chỉ dùng local:
- Service chỉ bind local.
- Có thể thêm API key nội bộ.
- Không cho phép truy cập từ mạng ngoài.
- Không public port ra internet.

---

## 6. Chức năng chi tiết

### 6.1. Dashboard
Mục tiêu:
- Hiển thị bức tranh tổng quan nhanh.

Thông tin hiển thị:
- Số lượng container đang chạy.
- Số lượng container đang dừng.
- Số lượng images.
- Số lượng volumes.
- Số lượng networks.
- Tài nguyên cơ bản.
- Danh sách compose project gần đây.
- Cảnh báo container lỗi hoặc exited bất thường.

Chức năng:
- Refresh nhanh.
- Nhấn để chuyển tới màn hình chi tiết tương ứng.

### 6.2. Quản lý Containers
Thông tin hiển thị:
- Container name
- Container ID
- Image
- Status
- Ports
- Created time
- CPU %
- Memory usage
- Restart policy
- Compose project/service (nếu có)

Chức năng:
- Refresh danh sách
- Start
- Stop
- Restart
- Remove
- Force remove
- Inspect
- Open logs
- Copy container ID/name
- Lọc theo:
  - running
  - exited
  - all
- Tìm theo tên/image
- Sort theo status, name, created time

Chức năng nâng cao:
- Batch start/stop/remove
- Open shell container
- Xem env variables
- Xem mount/volume mapping

### 6.3. Logs Viewer
Mục tiêu:
- Biến việc xem log thành trực quan và dễ debug hơn.

Thông tin/chức năng:
- Chọn container.
- Tải log gần nhất theo số dòng:
  - 100
  - 500
  - 1000
  - custom
- Realtime stream log.
- Pause/resume stream.
- Auto scroll.
- Tìm keyword.
- Highlight:
  - ERROR
  - WARN
  - INFO
  - DEBUG
- Copy selected log.
- Export log ra file.
- Clear màn hình viewer.
- Chuyển encoding khi cần.
- Filter theo keyword.

Mở rộng:
- Regex search
- Bookmark dòng log
- Multi-tab logs

### 6.4. Quản lý Images
Thông tin hiển thị:
- Repository
- Tag
- Image ID
- Size
- Created
- Dangling hay không

Chức năng:
- Refresh
- Remove image
- Remove unused images
- Prune dangling images
- Sort theo size
- Search theo repo/tag
- Xem image detail

Mở rộng:
- Pull image
- Tag image
- Export image
- Save/load image tar

### 6.5. Quản lý Docker Compose Projects
Mục tiêu:
- Thao tác nhanh với các project compose thường dùng.

Thông tin hiển thị:
- Danh sách project compose đã lưu
- Đường dẫn project
- Trạng thái các service
- Thời điểm chạy gần nhất

Chức năng:
- Add project compose
- Remove project khỏi favorites
- Detect file:
  - docker-compose.yml
  - compose.yml
- Up
- Down
- Restart
- Ps
- View logs theo service/project
- Open project folder
- Refresh project state

Mở rộng:
- Save profile môi trường
- Chạy kèm tham số:
  - build
  - force recreate
  - remove orphans
- Start một service cụ thể
- Stop một service cụ thể

### 6.6. Stats / Resource Monitor
Thông tin hiển thị:
- CPU %
- Memory usage
- Network RX/TX
- Block IO
- PIDs

Chức năng:
- Realtime update theo interval
- Chọn container
- Hiển thị chart đơn giản
- Top container theo CPU/memory

Mở rộng:
- Lưu snapshot
- So sánh stats giữa các container

### 6.7. Cleanup / Maintenance
Chức năng:
- Container prune
- Image prune
- Volume prune
- Network prune
- Builder prune
- System prune

Mục tiêu:
- Dọn nhanh tài nguyên thừa.
- Có xác nhận trước khi chạy.
- Hiển thị kết quả dọn dẹp.

Mở rộng:
- Ước lượng dung lượng có thể giải phóng
- Dọn theo từng loại
- Lưu log lịch sử cleanup

### 6.8. Settings
Thông tin cấu hình:
- Distro WSL đang dùng
- Host service URL
- API key
- Refresh interval
- Log tail mặc định
- Theme
- Compose favorite paths

Chức năng:
- Test kết nối service
- Test Docker connectivity
- Chọn distro
- Cấu hình tự khởi động service
- Cấu hình startup app

---

## 7. Yêu cầu kỹ thuật

### 7.1. Yêu cầu Windows App
- Chạy trên Windows 10/11.
- .NET 8.
- Giao diện WPF.
- Kiến trúc MVVM.
- Hỗ trợ dark mode hoặc theme hiện đại.
- Có logging nội bộ.

### 7.2. Yêu cầu WSL Service
- Chạy trong WSL2 Ubuntu.
- Có quyền truy cập Docker socket.
- Viết bằng Go.
- Chạy như local service/daemon.
- Có endpoint health check.
- Hỗ trợ graceful shutdown.
- Hỗ trợ log file riêng.

### 7.3. Yêu cầu Docker
- Docker Engine hoạt động trong WSL2.
- Docker CLI sẵn có.
- Docker Compose v2 hoạt động được.

---

## 8. Đề xuất cấu trúc dự án

### 8.1. WPF Solution

```text
DockerGui.sln
├── DockerGui.App
├── DockerGui.Core
├── DockerGui.Infrastructure
├── DockerGui.Contracts
└── DockerGui.Tests
```

Mô tả:
- **DockerGui.App**
  - WPF UI
  - Views, ViewModels, Resources, App.xaml
- **DockerGui.Core**
  - Models, interfaces, enums, business rules
- **DockerGui.Infrastructure**
  - API clients, settings persistence, logging
- **DockerGui.Contracts**
  - DTO request/response
- **DockerGui.Tests**
  - unit tests

### 8.2. Go Service

```text
wsl-docker-service/
├── cmd/server/
│   └── main.go
├── internal/
│   ├── api/
│   ├── docker/
│   ├── compose/
│   ├── logs/
│   ├── stats/
│   ├── config/
│   └── middleware/
├── pkg/
│   └── dto/
├── configs/
├── scripts/
└── go.mod
```

Mô tả:
- **api**
  - router, handlers
- **docker**
  - giao tiếp Docker SDK
- **compose**
  - wrapper cho docker compose
- **logs**
  - log stream xử lý riêng
- **stats**
  - stream số liệu realtime
- **config**
  - đọc config
- **middleware**
  - auth/logging/recover

---

## 9. Đề xuất API sơ bộ

### 9.1. Health
- `GET /api/health`

### 9.2. Containers
- `GET /api/containers`
- `GET /api/containers/{id}`
- `POST /api/containers/{id}/start`
- `POST /api/containers/{id}/stop`
- `POST /api/containers/{id}/restart`
- `DELETE /api/containers/{id}`
- `GET /api/containers/{id}/inspect`

### 9.3. Logs
- `GET /api/containers/{id}/logs?tail=200`
- `WS /ws/containers/{id}/logs`

### 9.4. Stats
- `GET /api/containers/{id}/stats`
- `WS /ws/containers/{id}/stats`

### 9.5. Images
- `GET /api/images`
- `DELETE /api/images/{id}`
- `POST /api/images/prune`

### 9.6. Compose
- `GET /api/compose/projects`
- `POST /api/compose/projects`
- `POST /api/compose/projects/up`
- `POST /api/compose/projects/down`
- `POST /api/compose/projects/restart`
- `GET /api/compose/projects/services`
- `GET /api/compose/projects/logs`

### 9.7. Cleanup
- `POST /api/system/prune`
- `POST /api/system/prune/images`
- `POST /api/system/prune/containers`
- `POST /api/system/prune/volumes`
- `POST /api/system/prune/networks`

---

## 10. Luồng hoạt động chính

### 10.1. Khởi động ứng dụng
1. Người dùng mở WPF app.
2. App kiểm tra service trong WSL có hoạt động không.
3. Nếu chưa chạy:
   - app có thể gợi ý start service
   - hoặc tự start bằng command WSL
4. App gọi health check.
5. Nếu Docker reachable, app load dashboard.

### 10.2. Luồng xem container
1. App gọi `GET /api/containers`
2. Service lấy dữ liệu từ Docker
3. App hiển thị DataGrid
4. Người dùng thao tác start/stop/restart/remove

### 10.3. Luồng xem logs
1. Người dùng chọn container
2. App load tail log ban đầu
3. App mở WebSocket stream
4. Log mới được append realtime vào giao diện

### 10.4. Luồng compose project
1. Người dùng lưu đường dẫn project
2. App gửi path đến service
3. Service chạy `docker compose ...`
4. App hiển thị kết quả

---

## 11. Rủi ro và điểm cần chú ý

### 11.1. Path giữa Windows và WSL
Đường dẫn project từ Windows cần chuyển thành path WSL hợp lệ.

Ví dụ:
- Windows:
  `C:\Users\doanq\Workspace\app`
- WSL:
  `/mnt/c/Users/doanq/Workspace/app`

Cần viết module chuyển đổi path chuẩn.

### 11.2. Quyền truy cập Docker
Service phải có quyền dùng Docker socket.
Cần bảo đảm user thuộc group docker hoặc cấu hình phù hợp.

### 11.3. Streaming logs/stats
Cần xử lý tốt:
- reconnect
- timeout
- mất kết nối service
- giới hạn buffer

### 11.4. Compose compatibility
Nên hỗ trợ:
- `docker-compose.yml`
- `compose.yml`
- compose v2

### 11.5. Đồng bộ trạng thái GUI
Cần tránh trường hợp:
- action xong nhưng UI chưa refresh
- stream nhiều gây lag UI
- data binding bị nghẽn khi log quá lớn

---

## 12. Đề xuất UI/UX

### 12.1. Bố cục chính
- Sidebar trái:
  - Dashboard
  - Containers
  - Images
  - Compose
  - Logs
  - Cleanup
  - Settings
- Khu vực nội dung chính:
  - DataGrid / charts / log viewer
- Thanh trên:
  - trạng thái service
  - trạng thái Docker
  - nút refresh nhanh
  - chọn WSL distro

### 12.2. Thiết kế trải nghiệm
- Ưu tiên thao tác 1-2 click.
- Hiển thị rõ trạng thái Running / Exited / Error.
- Màu log theo cấp độ.
- Có toast thông báo thao tác thành công/thất bại.
- Có loading rõ ràng khi gọi API.

---

## 13. Kế hoạch phát triển

### 13.1. Giai đoạn 1 - MVP
Mục tiêu:
- Có phiên bản dùng được cho các thao tác cơ bản nhất.

Phạm vi:
- Health check
- List containers
- Start / stop / restart / remove
- Logs tail + stream realtime
- List images
- Compose project basic:
  - add project
  - up
  - down
  - ps
- Cleanup cơ bản

Kết quả mong đợi:
- Thay thế phần lớn thao tác CLI thường ngày.
- Chạy ổn định trong môi trường cá nhân.

### 13.2. Giai đoạn 2 - Ổn định và hoàn thiện
Phạm vi:
- Stats realtime
- Search/filter/sort tốt hơn
- Batch actions
- Inspect detail
- Better error handling
- Settings đầy đủ
- Tự start service nếu chưa chạy
- Lưu favorites compose projects

Kết quả:
- Trải nghiệm tốt hơn cho dùng hằng ngày.
- Giảm phụ thuộc vào terminal.

### 13.3. Giai đoạn 3 - Nâng cao
Phạm vi:
- Multi-log tabs
- Export report
- Notifications
- Theme
- Open shell container
- View env / mounts / ports / networks chi tiết
- Lịch sử thao tác
- Tối ưu performance với log lớn

Kết quả:
- Ứng dụng trưởng thành hơn, gần với một Docker GUI chuyên dụng.

### 13.4. Giai đoạn 4 - Mở rộng hệ sinh thái
Phạm vi:
- Tích hợp quản lý WSL distro ở mức cơ bản
- Tích hợp local domain/proxy tool nếu cần trong tương lai
- Mở rộng thêm API tester, log viewer, env tools vào cùng nền tảng

Kết quả:
- Tiến tới một bộ Dev Toolbox tổng hợp.

---

## 14. Backlog đề xuất

### Ưu tiên cao
- Health check
- Container list
- Container actions
- Logs viewer
- Compose favorites
- Compose up/down
- Cleanup cơ bản

### Ưu tiên trung bình
- Stats realtime
- Image manager nâng cao
- Inspect JSON
- Notifications
- Search/filter nâng cao

### Ưu tiên thấp
- Shell vào container
- Theme system
- Export/import profile
- Snapshot môi trường
- Plugin/module system

---

## 15. Tiêu chí hoàn thành MVP

MVP được xem là đạt khi:
- Ứng dụng khởi động ổn định.
- WPF app kết nối được service trong WSL.
- Service kết nối được Docker Engine.
- Có thể:
  - xem containers
  - start/stop/restart/remove
  - xem logs realtime
  - xem images
  - chạy compose up/down/ps
  - chạy cleanup cơ bản
- Giao diện đủ dễ dùng cho thao tác hằng ngày.
- Không cần Docker Desktop.

---

## 16. Kết luận

Đây là một ứng dụng có tính thực tiễn cao đối với môi trường làm việc Windows + WSL2 + Docker. Việc chọn **WPF + MVVM** cho giao diện và **Go service trong WSL** cho lớp giao tiếp Docker là một hướng đi hợp lý vì:

- Tách biệt tốt giữa UI và hạ tầng.
- Dễ mở rộng về sau.
- Nhẹ hơn Docker Desktop.
- Phù hợp với workflow quản lý Docker bằng WSL2.
- Có thể phát triển dần từ MVP nhỏ đến một nền tảng dev tool mạnh hơn.

Kiến trúc này cũng mở đường cho việc mở rộng về sau sang các công cụ khác như:
- log viewer
- local proxy/domain manager
- API tester
- system cleanup/dev env fixer

Nếu phát triển đúng lộ trình, ứng dụng này không chỉ là một Docker GUI nhẹ mà còn có thể trở thành trung tâm quản lý môi trường phát triển trên máy cá nhân.
