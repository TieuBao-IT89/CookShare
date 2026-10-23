# Đánh giá công thức — issue #96

Đánh giá được lưu trong `RecipeRatings` theo tài khoản Identity và công thức. Mỗi tài khoản có tối đa một đánh giá cho mỗi món; gửi lần tiếp theo cập nhật số sao của hàng hiện có. Điểm trung bình và số lượt đánh giá được tính từ tất cả các hàng trong database. Các đánh giá cũ trong localStorage không được đọc, nhập tự động hay xóa.

## Sử dụng

1. Chạy `dotnet run --project CookShare/CookShare.csproj`, mở một công thức ở trang Khám phá hoặc bảng tin.
2. Đăng nhập bằng tài khoản khác tác giả. Trong **Món này có hợp vị bạn?**, chọn 1–5 sao rồi nhấn **Gửi đánh giá**.
3. Khi server xác nhận, điểm trung bình, số lượt và đánh giá của bạn được cập nhật. Chọn số sao khác và nhấn **Cập nhật đánh giá** để thay đổi; số lượt không tăng thêm.
4. Tải lại trang hoặc đăng xuất/đăng nhập lại: đánh giá vẫn còn. Dùng tài khoản thứ hai để đánh giá cùng món và kiểm tra điểm trung bình từ cả hai tài khoản.
5. Mở công thức do chính mình đăng: phần đánh giá chỉ hiển thị điểm cộng đồng và thông báo tác giả; không có biểu mẫu tự đánh giá.

Khách vẫn xem được điểm cộng đồng và có liên kết đăng nhập để đánh giá. Biểu mẫu dùng radio cùng nút gửi, hỗ trợ bàn phím và hoạt động khi tắt JavaScript. Khi bật JavaScript, gửi/cập nhật tại chỗ; lỗi mạng hoặc database không thay đổi điểm đã xác nhận và có thể thử lại. Khi đổi tài khoản hoặc hết phiên, trang yêu cầu tải lại/đăng nhập và loại bỏ biểu mẫu cũ.

Điểm được hiển thị trên trang chi tiết, thẻ công thức ở Khám phá/Profile/Đã lưu/công thức liên quan và bài trên bảng tin. UI làm tròn một chữ số thập phân; truy vấn vẫn dùng điểm trung bình thực tế để tính toán. Công thức chưa có lượt đánh giá hiển thị **Chưa có đánh giá**.

## Backend

- `GET /Recipe/{recipeId}/Ratings`: điểm trung bình, số lượt, đánh giá của tài khoản hiện tại và quyền chấm sao; không nhận UserId từ query string.
- `POST /Recipe/{recipeId}/Ratings`: form `Stars` là số nguyên 1–5 và `__RequestVerificationToken`; yêu cầu đăng nhập, từ chối tác giả với HTTP 403.
- Gửi `Accept: application/json` để nhận trạng thái đã lưu; biểu mẫu HTML thông thường quay lại `Recipe/Details/{id}#recipeRatingSection`.
- Recipe không tồn tại trả 404; dữ liệu sao/CSRF không hợp lệ trả 400. Lỗi database trả 503 với thông báo không chứa chi tiết kết nối.
- Phản hồi đặt `Cache-Control: no-store`. Truy vấn dùng aggregate và projection, tải thống kê theo batch cho các thẻ thay vì truy vấn riêng từng thẻ.
- Dùng unique index `IX_RecipeRatings_UserId_RecipeId` và check constraint `CK_RecipeRatings_Stars` hiện có. Nếu hai lượt gửi đầu tiên diễn ra đồng thời, yêu cầu thua unique index cập nhật hàng vừa được tạo; không thêm hàng thứ hai. Các cập nhật đồng thời tuân theo lần ghi cuối.

Không cần migration mới. Database phải có schema hiện hành của dự án.

## Kiểm thử

```powershell
dotnet test CookShare.sln --configuration Release

powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-RecipeRatingBrowserTests.ps1 -Viewport Desktop
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-RecipeRatingBrowserTests.ps1 -Viewport Mobile
```

Browser harness dùng Edge/Chrome, Python và profile tạm riêng; phản hồi API được kiểm soát, không truy cập tài khoản/database thật. Các bài kiểm tra bao gồm trạng thái đang lưu, CSRF, cập nhật các điểm trên trang, gửi trùng, lỗi mạng/503, phản hồi sai, hết phiên, đổi tài khoản, bfcache và không dùng localStorage.

Để kiểm tra PostgreSQL, cấu hình database kiểm thử riêng theo [RECIPE_SEEDING.md](RECIPE_SEEDING.md), bật `COOKSHARE_RUN_POSTGRES_TESTS=1` và `COOKSHARE_TEST_CONNECTION` rồi chạy `dotnet test`. Các bài PostgreSQL xác nhận ghi đồng thời, unique/check constraints và dữ liệu sau hai lần khởi động web host. Chỉ dùng database `cookshare_test` hoặc `cookshare_test_*`; không trỏ bài kiểm thử vào database Development/Production.
