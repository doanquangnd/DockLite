# Docker GUI (WPF + Go + WSL) – Roadmap theo tuần

## Tổng quan
- Kiến trúc:
  - WPF + MVVM (UI)
  - Go service trong WSL (Docker API)
- Mục tiêu: build Docker GUI nhẹ thay thế CLI & Docker Desktop cơ bản

---

## Bảng roadmap

| Tuần | Hạng mục chính | Công việc chi tiết | Đầu ra (Deliverable) | Độ ưu tiên |
|------|--------------|------------------|----------------------|-----------|
| 1 | Kiến trúc & Setup | - Tạo solution WPF (MVVM) <br> - Tạo Go service skeleton <br> - Thiết kế API contract <br> - Test kết nối WPF → WSL service | App gọi được `/health` | 🔴 Cao |
| 2 | Core service & UI shell | - Health check <br> - Docker info API <br> - Layout WPF (sidebar, main view) <br> - Settings cơ bản | UI có layout + trạng thái service/docker | 🔴 Cao |
| 3 | Containers | - API list/start/stop/restart/remove <br> - UI DataGrid containers <br> - Filter + search | Quản lý container cơ bản | 🔴 Cao |
| 4 | Logs realtime | - API logs + WebSocket <br> - UI log viewer <br> - Tail + search + highlight | Log viewer realtime usable | 🔴 Cao |
| 5 | Docker Compose | - API compose wrapper <br> - Add/remove project <br> - Up/down/ps <br> - Convert path Windows → WSL | Quản lý compose project | 🔴 Cao |
| 6 | Images + Cleanup | - API images <br> - Remove/prune <br> - UI cleanup actions | Dọn tài nguyên Docker | 🟠 Trung |
| 7 | Stabilization | - Error handling <br> - Retry logic (GET) <br> - UX polish <br> - Settings (timeout HTTP) | App ổn định, ít lỗi | 🔴 Cao |
| 8 | Release MVP | - Build exe (`scripts/Publish-Wpf.ps1`) <br> - Build Go (`scripts/Build-GoInWsl.ps1`) <br> - Start service (`scripts/Start-DockLiteWsl.ps1`) <br> - README phát hành | MVP usable | 🔴 Cao |

---

## Roadmap mở rộng (sau MVP)

| Tuần | Hạng mục | Công việc | Đầu ra | Độ ưu tiên |
|------|--------|----------|--------|-----------|
| 9 | Stats | - API stats realtime <br> - UI chart CPU/memory | Monitoring container | 🟠 Trung |
| 10 | Inspect + Shell | - Inspect JSON <br> - Env, mounts <br> - Open shell | Debug mạnh hơn | 🟡 Thấp |
| 11 | Productivity | - Multi log tabs <br> - Batch actions <br> - Notifications | UX tốt hơn | 🟠 Trung |
| 12 | Platform hóa | - Profiles project <br> - Modular structure <br> - Chuẩn bị tích hợp tool khác | Dev toolbox nền tảng | 🟡 Thấp |

---

## Mốc quan trọng

| Mốc | Thời điểm | Nội dung |
|-----|----------|--------|
| Mốc A | Tuần 2 | App kết nối service OK |
| Mốc B | Tuần 4 | Container + Logs usable |
| Mốc C | Tuần 6 | Compose + Cleanup |
| Mốc D | Tuần 8 | MVP release |

---

## Ưu tiên tổng thể

### 🔴 Bắt buộc (MVP)
- Health + connectivity
- Containers
- Logs
- Compose
- Cleanup

### 🟠 Nên có
- Images
- Stats
- Error handling tốt

### 🟡 Nice-to-have
- Shell
- Notifications
- Theme
- Profiles

---

## Gợi ý cách làm mỗi tuần

- 20%: design
- 60%: coding
- 20%: test + refactor

---

## Rủi ro cần xử lý sớm

- Path Windows ↔ WSL
- Startup service
- WebSocket logs performance
- Docker compose output parsing

---

## Kết luận

Roadmap này giúp:
- Ra MVP sau 8 tuần
- Có thể dùng thực tế
- Dễ mở rộng thành Dev Toolbox
