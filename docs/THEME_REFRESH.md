# Theme refresh — 17/11/2026

Thay menu chọn giao diện bằng công tắc mặt trời/mặt trăng; làm mới cả hai theme
trên nền Premium V2 (`1c68005`). [Ảnh trước/sau](ui/theme-refresh/README.md).

## Thiết kế

- **Sage & Cream**: nền kem, sage và điểm nhấn đào; gradient nền nhiều lớp,
  vùng chào mừng và khám phá có chuyển màu, card có viền sáng và bóng mềm.
- **Aurora Kitchen**: nền xanh đêm, bề mặt teal, mint và ánh đồng; cùng hệ token
  để feed, discovery, profile, editor, tài khoản và footer nhất quán.
- Header/bottom navigation dùng nền kính nhẹ; gradient nằm ở nền và vùng nhấn,
  vùng đọc vẫn giữ màu chữ rõ ràng. Header 360px thu gọn wordmark và khoảng cách.
- Tham khảo [Uiverse — reading-mode switch](https://uiverse.io/uiverse-astronaut/young-firefox-56)
  cho nút trượt sun/moon và [Linear — new UI](https://linear.app/changelog/2024-03-20-new-linear-ui)
  cho phân lớp bề mặt. CSS/Razor/JavaScript được viết riêng cho CookShare;
  không sao chép component hay thêm React, package, font hoặc icon runtime mới.

## Hành vi

Nút `role="switch"`, tên truy cập ổn định **Giao diện tối**, dùng `aria-checked`
để phản ánh theme đang áp dụng. Click, Space hoặc Enter đổi sáng/tối và lưu
lựa chọn. Nút màn hình bên cạnh chọn **Theo giao diện thiết bị**, dùng
`aria-pressed` để báo chế độ tự động; cả hai nút có vùng chạm ít nhất 44px.

`theme-init.js` chạy trong head trước CSS. Giữ khóa `cookshare.theme` với
`light` / `dark` / `system`, tương thích lựa chọn cũ. Theme và trạng thái nút
đồng bộ qua các tab; giá trị sai hoặc bị xóa trở về theo thiết bị. Khi chọn
sáng/tối thủ công, thay đổi hệ điều hành không ghi đè lựa chọn đó. Storage bị
chặn vẫn cho phép chuyển trong trang hiện tại. Theme chỉ lưu preference hiển thị.

Không JavaScript: ẩn hai nút, CSS theo thiết bị. `prefers-reduced-motion` tắt
animation/chuyển động, kể cả thumb của công tắc. Trạng thái chọn được thông báo
qua live region. In trang ẩn điều hướng.

## Kiểm chứng

- `dotnet build CookShare.sln --configuration Release --no-restore`: thành công,
  0 lỗi, 21 warning đã có từ baseline.
- `dotnet test CookShare.sln --configuration Release --no-build --no-restore`:
  **614 passed, 0 failed, 0 skipped**, bật PostgreSQL và Chrome browser opt-in.
  Database test riêng `cookshare_test_theme` trên loopback, tách fixture UI.
- `python tools/checks/premium-ui.py --base http://127.0.0.1:5098 --out artifacts/theme-refresh`:
  **154 tổ hợp** (11 trang × 7 viewport × 2 theme), **23 behavioral checks**,
  không tràn ngang hoặc uncaught JS error. Viewport 360/390/768/960/1024/1366/1920px.
- `python tools/checks/premium-interactions.py --base http://127.0.0.1:5098`:
  **22 ca đạt**, gồm CRUD, scaler/checklist/cooking, like/comment/save/rating,
  follow/notification, CSRF, lưu theme qua điều hướng và dọn công thức kiểm thử.
- `dotnet format whitespace`, `style`, `analyzers` với `--verify-no-changes --no-restore`:
  đạt; style/analyzers dùng `--severity warn`. `node --check` và `git diff --check`: đạt.
- Sáu cặp token ink/white, muted/white, primary/white, accent-ink/white,
  muted/light và on-primary/primary có tương phản thấp nhất **4,59:1** ở light,
  **6,00:1** ở dark. Đây là đo token màu, không phải chứng nhận WCAG toàn site.

Ảnh, kết quả browser đầy đủ và TRX nằm trong `artifacts/theme-refresh/` và
`CookShare.Tests/TestResults/` (ignored). Ảnh review chọn lọc được lưu trong docs.
Chưa kiểm tra Safari/iOS/Android vật lý.

## Đưa lên main

Maintainer yêu cầu push, tạo PR và merge vào `main`. Nhánh hiện tại kế thừa
Social 2.0, Premium V2 và các thay đổi từ `develop` chưa có trên `main`, vì vậy
PR phát hành phải mô tả toàn bộ phạm vi đó, bao gồm migrations đã có.

Riêng theme refresh không thêm migration hoặc cấu hình. Khi triển khai từ main
cũ, cần áp dụng đầy đủ migrations theo [Setup](SETUP.md) và quy trình vận hành,
đặc biệt schema Social 2.0/search. Lần kiểm chứng này chỉ dùng PostgreSQL cô lập;
không cập nhật database ứng dụng. Merge vào main không tự chạy workflow Azure
Staging, vốn chỉ chạy khi push `develop` hoặc được gọi thủ công trên `develop`.
