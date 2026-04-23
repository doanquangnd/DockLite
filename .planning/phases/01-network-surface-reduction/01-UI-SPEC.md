# UI-SPEC: Phase 1 — Cảnh báo mức độ bảo mật cho Base URL (Cài đặt)

**Phase:** 1 — Network Surface Reduction
**Màn hình:** Cài đặt → phần kết nối tới dịch vụ (đã tồn tại, `SettingsView.xaml`).

## Mục tiêu trực quan

- Giữ trường nhập `ServiceBaseUrl` như hiện tại.
- Thay thế cảnh báo đơn lớp bằng hệ thống màu tối thiểu hai mức (cảnh báo so với nghiêm trọng), ánh xạ tới cùng bản copy tiếng Việt do ViewModel tạo (không mã hóa copy trong XAML ngoài resource i18n nếu sau này tách).
- Tương phản: chữ cảnh báo nền nền vùng cài đặt đạt tối thiểu WCAG AA 4.5:1 ở cả sáng và tối.

## Mức độ và màu

| Mức | Điều kiện (tóm tắt) | Brush tài nguyên |
|-----|----------------------|------------------|
| Không cảnh báo | Host là loopback (`localhost` / `127.0.0.1` / `::1`) | Ẩn hoặc chuỗi rỗng |
| Cảnh báo | Host **không** loopback **và** scheme an toàn hơn (`https` hoặc `wss` nếu áp dụng) | `ThemeWarningForegroundBrush` (đã có) |
| Nghiêm trọng | Host **không** loopback **và** scheme `http` (hoặc `ws` nếu sau này dùng) | `ThemeDangerForegroundBrush` (mới, cần thêm vào `ModernTheme.xaml` và `DarkTheme.xaml`) |

Ghi chú: Khi cả `http` lẫn `https` chưa triển khai phía dịch vụ trong phase này, ưu tiên phân tầng dựa trên **scheme thực tế** người dùng nhập trong Base URL (đa số vẫn `http` tới dịch vụ HTTP hiện tại) — tầng nghiêm trọng sẽ kích hoạt thường xuyên hơn khi mở LAN, phù hợp cảnh báo ROADMAP.

## Hành vi tương tác

- Thay đổi văn bản URL cập nhật ngay cảnh báo (`UpdateSourceTrigger=PropertyChanged` — giữ pattern hiện tại).
- Nút "Điền IP WSL" không đổi; sau khi điền, nếu URL trỏ tới IP không phải loopback, cảnh báo phải xuất hiện tương ứng mức.

## Ngoài phạm vi giao diện phase này

- Bố cục lại toàn bộ màn cài đặt, bảng màu sản phẩm, i18n Anh mới (chỉ bổ sung chuỗi mới nếu copy được chuyển sang resource; phase n1 có thể gộp mô tả trong `SettingsViewModel` theo cách cũ trước, rồi bổ sung i18n sau).

## Liên kết yêu cầu

- `NET-04` (REQUIREMENTS.md), thành công ROADMAP mục 3–4 (banner vàng/đỏ).
