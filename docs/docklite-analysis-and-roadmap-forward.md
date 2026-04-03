# Phân tích và đánh giá DockLite (hiện trạng) — hướng phát triển tiếp theo

Tài liệu tổng hợp đánh giá kiến trúc, mức độ trưởng thành so với roadmap, điểm mạnh, rủi ro và gợi ý ưu tiên phát triển. Bổ sung cho `docker_gui_roadmap.md`, `docklite-optimization-and-extensions.md` và `docklite-improvement-plan.md`.

---

## 1. Kiến trúc và vai trò từng phần

- **Windows (DockLite.App):** UI WPF, điều phối HTTP/WebSocket tới service, cấu hình local (`settings.json`), toast, nhật ký ứng dụng.
- **WSL (wsl-docker-service):** Go gọi Docker Engine qua `docker` CLI; lắng nghe HTTP (và WebSocket cho log); Compose, Image, System prune qua các endpoint mô tả trong README gốc.
- **Tách lớp:** `DockLite.Contracts` (API), `DockLite.Core` (cấu hình, version), `DockLite.Infrastructure` (HTTP client, WSL, path) — đủ để mở rộng mà không gom logic vào code-behind.

**Điểm mạnh kiến trúc:** Ranh giới rõ: UI không chạy Docker trực tiếp; mọi thao tác Docker đi qua một service có thể kiểm soát và ghi log. Phù hợp mục tiêu “GUI nhẹ” thay cho Docker Desktop đầy đủ.

---

## 2. Đánh giá mức độ trưởng thành (so roadmap MVP)

Theo `docs/docker_gui_roadmap.md`, MVP (tuần 1–8) gồm health, Docker info, containers, logs (kể cả WebSocket), compose, images/cleanup, ổn định hóa, publish. README hiện tại đã liệt kê đủ nhóm endpoint cốt lõi; phần Cài đặt WSL (đường dẫn, distro, đồng bộ mã, wslpath) được làm sâu thêm so với roadmap gốc.

**Kết luận:** Về **phạm vi tính năng lõi**, ứng dụng đã vượt MVP tối thiểu và đang ở giai đoạn **củng cố UX, độ tin cậy WSL/path, và tối ưu hiệu năng** — phù hợp nội dung trong `docklite-optimization-and-extensions.md`.

---

## 3. Điểm mạnh thực tế

- **Tích hợp WSL:** Đã có xử lý UNC, `wslpath`, chuẩn hóa đường dẫn ổ đĩa (`C:/...`), thông báo lỗi kèm stderr, tách ô nguồn/đích đồng bộ — giảm hiểu nhầm so với một ô cấu hình chung.
- **Tự khởi động service và chờ health:** Giúp người mới không phải nhớ lệnh tay (vẫn cần README khi build Go lần đầu).
- **Bề mặt API Go:** Đủ rộng cho container, image, compose, cleanup; client có thể mở rộng UI dần mà không đổi mô hình “một service trung gian”.
- **Tài liệu nội bộ:** Roadmap, improvement plan, optimization doc hỗ trợ ưu tiên công việc có căn cứ.

---

## 4. Rủi ro và hạn chế

- **Phụ thuộc WSL và cấu hình máy:** IP WSL đổi, nhiều distro, quyền file — vẫn là nguồn hỗ trợ/khiếu nại chính; giao diện chỉ giảm, không loại trừ hoàn toàn.
- **Bảo mật:** HTTP tới service (thường LAN hoặc localhost); TLS/auth không nêu trong luồng mặc định — chấp nhận được cho máy cá nhân, cần cân nhắc nếu mở ra mạng không tin cậy.
- **Kiểm thử tự động:** Test .NET hiện tập trung contract và path; ViewModel và tích hợp end-to-end còn ít — refactor lớn sẽ cần kiểm tra tay nhiều hơn.
- **ViewModel lớn** (Containers, Settings): Dễ bảo trì hơn nếu tách dần dịch vụ trung gian khi thêm tính năng (đã ghi nhận trong doc tối ưu).

---

## 5. Hướng phát triển tiếp theo (gợi ý ưu tiên)

### 5.1 Ngắn hạn (cải thiện trải nghiệm, ít đổi kiến trúc)

- Tối ưu **theo tab và trạng thái cửa sổ** (giảm poll khi không foreground hoặc khi không ở trang Container/Dashboard) — thống nhất với `docklite-optimization-and-extensions.md`.
- **Rà soát `DockLiteApiClient`:** đảm bảo response có body lớn luôn dispose stream đúng cách.
- **Cài đặt / WSL:** Cân nhắc “trang kiểm tra nhanh” (wslpath, uname, health) gom các thao tác hiện có thành một luồng rõ ràng cho người mới.

### 5.2 Trung hạn (giá trị sản phẩm)

- **Inspect / chi tiết container:** Mở rộng bảng mount, port, env; filter theo label nếu có nhu cầu.
- **Compose:** Hỗ trợ `-f` hoặc nhiều file compose khi người dùng phản hồi thường xuyên.
- **Stats:** Roadmap mở rộng (tuần 9–10) — biểu đồ hoặc sparkline; cân nhắc WebSocket/SSE cho một container đang mở để giảm polling.

### 5.3 Dài hạn / sản phẩm hóa

- **Cài đặt và cập nhật:** Installer (MSIX, click-once hoặc script kèm kiểm tra .NET runtime), kiểm tra phiên bản DockLite và service.
- **Bảo mật tùy chọn:** Token hoặc ràng buộc chỉ `127.0.0.1` kèm cảnh báo UI khi base URL không phải localhost.

---

## 6. Tóm tắt

DockLite hiện là **client Docker qua WSL** khá đầy đủ cho nhu cầu phát triển cá nhân. **Điểm khác biệt và cũng là phần tốn công nhất** là tích hợp WSL và đường dẫn — đã được củng cố đáng kể. **Bước tiếp theo có lợi nhất** thường là: tối ưu hiệu năng và vòng đời UI, làm sâu inspect/compose/stats, và tăng mức “sản phẩm” (cài đặt, kiểm thử, bảo mật tùy chọn) thay vì chỉ mở rộng thêm API Docker mới ngay.

---

## 7. Tham chiếu trong repo

| Tài liệu | Nội dung chính |
|----------|----------------|
| `docs/docker_gui_roadmap.md` | Roadmap theo tuần, MVP và mở rộng |
| `docs/docker_gui_wpf_go_wsl_analysis.md` | Phân tích kiến trúc ban đầu |
| `docs/docklite-optimization-and-extensions.md` | Tối ưu hiệu năng, WSL, Go, mở rộng module |
| `docs/docklite-improvement-plan.md` | Kế hoạch cải tiến chi tiết theo mục |
| `README.md` | Endpoint API, build, cài đặt, gỡ lỗi WSL |

Ngày cập nhật nội dung phân tích này: tháng 3 năm 2026.
