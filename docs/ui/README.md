# CookShare UI/UX — issue #85

Branch: `style/improve-overall-ui` → `develop`.

## Kết quả và phạm vi

Giao diện dùng tông xanh lá, nền kem và typography thống nhất. Home tập trung vào tìm món ăn, danh mục và công thức; các trang Khám phá, Chi tiết, Create/Edit, Đã lưu, Profile, Login/Register và Error dùng chung phong cách này.

Các vấn đề trước khi nâng cấp: Home dùng bài đăng viết cứng, header luôn đánh dấu Trang chủ, tìm kiếm trên header chưa hoạt động, CSS tài khoản nằm trong view, Create/Edit lặp biểu mẫu và trạng thái Đã lưu trống hiển thị “404”. Layout mới sửa các điểm đó, có menu mobile, liên kết danh mục/tìm kiếm hoạt động và các trạng thái tải, trống, lỗi phù hợp.

Controllers, model, API, database và các khóa lưu trữ không thay đổi. Các luồng công thức, thích, lưu và bình luận tiếp tục dùng dữ liệu trên trình duyệt. Việc ghi công thức chỉ báo thành công sau khi lưu được; dữ liệu hỏng không bị ghi đè bằng một danh sách mới.

## Design system và tái sử dụng

| Thành phần | Quy ước |
| --- | --- |
| Màu chính | `--primary: #245c43`, hover `#173f2e` |
| Nền / chữ / viền | `#faf9f5` / `#243b30` / `#e4e7df` |
| Màu phụ / lỗi | `#be6d45` / `#b33939` |
| Typography | Be Vietnam Pro cho nội dung, Playfair Display cho tiêu đề; có font dự phòng |
| Spacing | Các token 4, 8, 12, 16, 24, 32, 48, 64 px |
| Border radius | 8 / 14 / 24 px |
| Shadow / transition | `--shadow-sm`, `--shadow`, `--transition: 180ms ease` |
| Recipe Card | Ảnh 4:3, tên món, danh mục, nguyên liệu, tác giả, thích/lưu và liên kết chi tiết |
| Accessibility | Focus rõ ràng, skip link, label, ARIA trạng thái, menu Escape, dialog bàn phím, reduced motion |

- `wwwroot/css/style.css`: tokens, layout, thành phần dùng chung, Home, tài khoản và Profile.
- `wwwroot/css/recipe.css`: Khám phá, biểu mẫu và Chi tiết; giữ các token `--rc-*` mà module bình luận đang dùng.
- `_RecipeCardTemplate.cshtml` + `renderRecipeCard`: một template cho Home, Khám phá, Đã lưu và Profile.
- `_RecipeForm.cshtml`: Create/Edit dùng chung biểu mẫu và các ID hiện có.
- `_RecipeHeader`, `_AuthAside`, `_CategoryOptions`, `_RecipeLoading`, `_Icons`: các partial dùng chung.
- `style.js`: menu, toast, dialog xác nhận, ảnh dự phòng và tương tác biểu mẫu tài khoản.

Ảnh mẫu được đóng gói dưới dạng WebP trong `wwwroot/img/recipes`. Các URL ảnh mẫu trong dữ liệu cũ được ánh xạ tới ảnh cục bộ khi hiển thị; không cần xóa hoặc di chuyển localStorage. Ảnh lỗi có SVG dự phòng. Ảnh thẻ tải lazy và có kích thước cố định để hạn chế dịch chuyển bố cục.

## Ảnh Before / After

Ảnh chụp từ Chrome, desktop **1440 × 1000** và mobile **390 × 844**, trên profile kiểm thử riêng. Các ảnh viewport giữ cùng kích thước để đối chiếu. Xem thêm ảnh đầy đủ ở cuối trang Chi tiết và các trạng thái sau thao tác trong thư mục `after`.

| Trang | Before desktop / mobile | After desktop / mobile |
| --- | --- | --- |
| Home | [Desktop](before/home-desktop.png) · [Mobile](before/home-mobile.png) | [Desktop](after/home-desktop.png) · [Mobile](after/home-mobile.png) |
| Khám phá / Search | [Desktop](before/explore-desktop.png) · [Mobile](before/explore-mobile.png) | [Desktop](after/explore-desktop.png) · [Mobile](after/explore-mobile.png) |
| Recipe Details | [Desktop](before/details-desktop.png) · [Mobile](before/details-mobile.png) | [Desktop](after/details-desktop.png) · [Mobile](after/details-mobile.png) |
| Create / form chung Edit | [Desktop](before/create-desktop.png) · [Mobile](before/create-mobile.png) | [Desktop](after/create-desktop.png) · [Mobile](after/create-mobile.png) |
| Favorites trống | [Desktop](before/saved-desktop.png) · [Mobile](before/saved-mobile.png) | [Desktop](after/saved-desktop.png) · [Mobile](after/saved-mobile.png) |
| Profile | [Desktop](before/profile-desktop.png) · [Mobile](before/profile-mobile.png) | [Desktop](after/profile-desktop.png) · [Mobile](after/profile-mobile.png) |
| Login | [Desktop](before/login-desktop.png) · [Mobile](before/login-mobile.png) | [Desktop](after/login-desktop.png) · [Mobile](after/login-mobile.png) |
| Register | [Desktop](before/register-desktop.png) · [Mobile](before/register-mobile.png) | [Desktop](after/register-desktop.png) · [Mobile](after/register-mobile.png) |

Ảnh bổ sung: [Favorites có công thức](after/saved-populated-desktop.png), [Favorites mobile](after/saved-populated-mobile.png), [Details đầy đủ](after/details-full.png), [Create đầy đủ](after/create-full.png).

## Kiểm tra

```powershell
dotnet test CookShare.sln --configuration Release --no-restore
dotnet format whitespace CookShare.sln --verify-no-changes --no-restore
dotnet format style CookShare.sln --verify-no-changes --no-restore --severity warn
dotnet format analyzers CookShare.sln --verify-no-changes --no-restore --severity warn
```

Để chạy kiểm tra trình duyệt, cài Playwright vào thư mục bị Git bỏ qua; script dùng Chrome đã có trên máy, không cần cài browser mới:

```powershell
python -m pip install --target artifacts/qa-deps playwright
dotnet run --project CookShare/CookShare.csproj --urls http://127.0.0.1:5088
# Trong một terminal khác:
python tools/ui_review.py verify
python tools/ui_review.py after
```

`verify` kiểm tra tìm kiếm từ header, xóa từ khóa, bộ lọc, kết quả trống, thích/lưu qua nhiều trang, checklist nguyên liệu, bình luận, validation, upload ảnh, Create/Edit/Delete và xác nhận xóa bằng bàn phím. Kiểm tra bố cục bao gồm 10 route ở 320, 390, 768, 1024 và 1440 px; kiểm tra lỗi bao gồm công thức không tồn tại, Home trống, dữ liệu hỏng và lỗi ghi localStorage. Kết quả máy đọc được nằm trong [verification.json](verification.json).

## Giới hạn hiện có

- Login/Register vẫn chưa có backend xác thực. Submit hợp lệ hiển thị thông báo đúng trạng thái; không tạo tài khoản, phiên đăng nhập hoặc lưu mật khẩu. Hiện/ẩn mật khẩu, email và xác nhận mật khẩu có validation.
- Profile vẫn dùng thông tin thành viên minh họa; góc công thức hiển thị bộ sưu tập trên thiết bị, không khẳng định quyền sở hữu từng món khi chưa có authentication.
- Font Google có font dự phòng. Ảnh tự nhập từ URL bên ngoài vẫn phụ thuộc nguồn ảnh đó.
- Chưa merge; PR hướng tới `develop` và chờ phân công reviewer theo yêu cầu.

## Nguồn ảnh

Ảnh công thức và avatar giữ nguồn Unsplash đã dùng trong giao diện trước; ảnh căn bếp được bổ sung cho phần tài khoản. Các bản WebP được lưu cục bộ, Home dùng lại ảnh salad.

| Tệp | Nguồn |
| --- | --- |
| pho.webp | [Unsplash](https://images.unsplash.com/photo-1582878826629-29b7ad1cdc43) |
| chicken-rice.webp | [Unsplash](https://images.unsplash.com/photo-1569058242253-92a9c755a0ec) |
| pasta.webp | [Unsplash](https://images.unsplash.com/photo-1621996346565-e3dbc646d9a9) |
| salad.webp | [Unsplash](https://images.unsplash.com/photo-1512621776951-a57141f2eefd) |
| tiramisu.webp | [Unsplash](https://images.unsplash.com/photo-1571877227200-a0d98ea607e9) |
| peach-tea.webp | [Unsplash](https://images.unsplash.com/photo-1499638673689-79a0b5115d87) |
| kitchen.webp | [Unsplash](https://images.unsplash.com/photo-1556911220-bff31c812dba) |
| profile-avatar.webp | [Unsplash](https://images.unsplash.com/photo-1494790108377-be9c29b29330) |
