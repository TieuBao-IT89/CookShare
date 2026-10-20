## Thay đổi

CookShare trước đây dùng giao diện rời rạc giữa các trang, tìm kiếm ở header chưa hoạt động và Home hiển thị công thức viết cứng. PR thống nhất design system xanh lá/nền kem, nâng cấp Home, Khám phá, Details, Create/Edit, Favorites, Profile, Login/Register và Error; menu mobile và các liên kết tìm kiếm/danh mục hoạt động xuyên trang.

Recipe Card và biểu mẫu Create/Edit dùng chung Razor partial. Bổ sung trạng thái tải/trống/lỗi, focus và dialog xóa hỗ trợ bàn phím, thông báo lưu thất bại, ảnh WebP cục bộ và ảnh dự phòng. Giữ controllers, model, database và các khóa localStorage hiện có.

Tích hợp `develop` mới nhất, giữ đánh giá sao (#82) và theo dõi người dùng (#83): điểm sao trên thẻ/Details, kết nối cộng đồng ở Home, Follow/Unfollow, bộ đếm và các tab người theo dõi/đang theo dõi ở Profile.

Closes #85

## Validation

- Build Release và 21 unit tests .NET 8 đã pass.
- Kiểm tra whitespace, style và analyzers theo workflow CI.
- Kiểm tra Chrome đã pass: tìm kiếm/lọc, thích/lưu, đánh giá sao, theo dõi, các tab Profile bằng bàn phím, bình luận, checklist, upload, Create/Edit/Delete, validation tài khoản và trạng thái lỗi lưu trữ.
- 10 route × 5 chiều rộng (320–1440 px), không tràn ngang; không có lỗi JavaScript trong lượt kiểm tra.
- [Ảnh Before/After và design system](https://github.com/TieuBao-IT89/CookShare/blob/style/improve-overall-ui/docs/ui/README.md), [báo cáo trình duyệt](https://github.com/TieuBao-IT89/CookShare/blob/style/improve-overall-ui/docs/ui/verification.json).

Login/Register vẫn là giao diện, chưa có backend xác thực; submit không tạo tài khoản hoặc lưu mật khẩu. Thông tin Profile vẫn là dữ liệu minh họa.

Base: `develop`. Chờ phân công reviewer; chưa merge.
