# Bảng tin CookShare — issue #98

Trang chủ ưu tiên công thức từ cộng đồng với bố cục bảng tin, thay cho các khu vực công thức lặp lại. Desktop có cột điều hướng, bài đăng ở giữa và gợi ý cộng đồng; điện thoại tập trung vào một cột bài đăng.

## Icon giao diện

Layout dùng chung tải [Font Awesome Free 7.3.1 từ cdnjs](https://cdnjs.com/libraries/font-awesome), cố định phiên bản và kiểm tra SRI. Bảng tin, danh mục món ăn, nút tương tác và thanh điều hướng dùng icon Free qua CSS/webfont; không cần Kit hoặc API key. Trình duyệt cần truy cập `cdnjs.cloudflare.com` để tải CSS và font.

Thêm icon theo dạng `<i class="icon fa-solid fa-utensils" aria-hidden="true"></i>` hoặc `fa-regular` cho kiểu nét viền. Giữ nhãn chữ cho thao tác; nút chỉ có icon cần `aria-label`. Icon trái tim/dấu lưu đổi từ viền sang đặc theo trạng thái server. Logo đầu bếp vẫn dùng SVG của CookShare. Tra tên và kiểu icon Free tại [Font Awesome](https://fontawesome.com/search?ic=free).

## Hai bảng tin

- `/` hoặc `/?feed=latest`: công thức mới nhất của cộng đồng, khách chưa đăng nhập vẫn xem được.
- `/Feed/Following` hoặc `/?feed=following`: công thức của những tài khoản người đang đăng nhập theo dõi. Khách được chuyển tới đăng nhập, có return URL để quay lại.

Danh sách Following được đọc từ `Follows` trong PostgreSQL với `FollowerId` từ claims đăng nhập. Query string không thể chọn người xem khác. Bài của chính mình và của người chưa theo dõi không xuất hiện trong Following, trừ khi có quan hệ Follow tương ứng trong dữ liệu. Khi chưa theo dõi ai hoặc những người đó chưa đăng công thức, bảng tin có thông báo và liên kết khám phá, không tự trộn công thức công khai vào feed riêng.

Từ cột gợi ý hoặc một bài đăng, mở Profile tác giả và dùng chức năng Theo dõi hiện có. Quay lại **Đang theo dõi** để xem công thức của họ. Follow/Unfollow đã lưu trong database được áp dụng ở những lần truy vấn tiếp theo.

## Tải thêm và tương tác

- Mỗi lượt tối đa 8 công thức, sắp xếp `CreatedAt DESC, Id DESC`. Cursor mang thời gian/ID của công thức cuối; bài mới xuất hiện trong lúc cuộn không làm lặp các bài đã xem. Tải lại đầu bảng tin để nhận những bài mới đó.
- Khi cuộn gần cuối, JavaScript tải thêm qua `GET /Feed/More`. Sau tối đa 3 lượt tự động, dùng **Xem thêm món ngon** để tiếp tục và giữ chân trang dễ truy cập. Có thể bỏ chọn **Tự tải thêm**.
- Khi tắt JavaScript, **Xem thêm** là liên kết chuyển sang trang kế tiếp được render từ server.
- **Like** và **Lưu** dùng các API/CSRF hiện có, kể cả với bài vừa tải thêm. Trạng thái lấy từ database và được giữ khi tải lại trang.
- **Bình luận** mở ngay dưới bài, chỉ tải dữ liệu khi cần qua `GET /Feed/Comments/{recipeId}`. Ban đầu hiển thị 5 bình luận mới nhất; liên kết **Xem tất cả bình luận** dẫn tới trang chi tiết. Tạo/sửa/xóa dùng quyền sở hữu và endpoint hiện có. Khi tắt JavaScript, nút Bình luận mở trang chi tiết.
- **Chia sẻ** mở trình chia sẻ của thiết bị nếu có, hoặc sao chép link. Khi trình duyệt không cho phép clipboard, hộp thoại cung cấp liên kết để sao chép thủ công.
- Mô tả dài có thể mở rộng. Thời gian bài đăng được trình bày theo trình duyệt; thời gian đầy đủ có trong tooltip. Nút **Về đầu** xuất hiện sau khi cuộn xuống.

Truy vấn feed dùng projection thông tin công khai, số Like/Comment và trạng thái Like/Save, không tải PasswordHash, SecurityStamp hoặc Email. Mỗi lượt chỉ lấy 9 dòng để xác định còn trang tiếp theo, trả tối đa 8. Không có truy vấn riêng cho từng bài và không có nguồn dữ liệu feed trong localStorage.

Các phản hồi feed và bình luận đặt `Cache-Control: no-store`. API tải thêm Following trả 401 nếu hết phiên. Frontend không ghép dữ liệu từ hai tài khoản khác nhau; khi phát hiện tài khoản đổi, yêu cầu tải lại. Khi quay lại bằng bfcache, trang tải lại để xác nhận phiên hiện tại.

## Kiểm tra thủ công

1. Chạy website với cấu hình Development đã có. Mở `/`, xác nhận bài đăng xuất hiện ngay dưới lời chào và ô chia sẻ công thức.
2. Cuộn xuống để tải thêm; kiểm tra mỗi bài chỉ xuất hiện một lần, có trạng thái đang tải/hết dữ liệu và nút Xem thêm.
3. Đăng nhập tài khoản A. Mở Profile B, nhấn Theo dõi. Mở `/Feed/Following`: chỉ các món của những tài khoản A theo dõi được hiển thị.
4. Bỏ theo dõi B, tải lại Following. Công thức B không còn ở bảng tin này; `/` vẫn hiển thị công thức công khai như bình thường.
5. Thử Like, Lưu, đăng bình luận và chia sẻ trên một bài được tải ở lượt thứ hai. Tải lại để xác nhận dữ liệu đã lưu. Kiểm tra sửa/xóa bình luận chỉ dành cho chính người viết.
6. Dùng cửa sổ ẩn danh mở `/Feed/Following`: phải chuyển tới Login, còn `/` vẫn mở được.
7. Trong DevTools bật Offline sau khi trang tải, chọn Xem thêm. Những bài đã xem được giữ lại; tắt Offline rồi thử lại.
8. Kiểm tra ở 390px, 768px và 1440px, bàn phím, ảnh lỗi/placeholder, không có bài, tài khoản chưa Follow và `prefers-reduced-motion`.

Animation chỉ dùng chuyển động ngắn cho bài mới và trạng thái tương tác, hỗ trợ reduced motion. Bài đăng không phụ thuộc vào animation để hiển thị.

## Kiểm thử tự động

Tại thư mục gốc repository:

```powershell
dotnet test CookShare.sln --configuration Release

powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-HomeFeedBrowserTests.ps1 -Viewport Desktop
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-HomeFeedBrowserTests.ps1 -Viewport Mobile
```

Browser harness cần Python và Chrome/Edge, dùng server loopback/profile tạm riêng. Fetch được điều khiển trong harness, không kết nối database hay dùng tài khoản thật. Các bài integration ASP.NET kiểm tra đăng nhập, thứ tự/phân trang, nội dung escape, trạng thái tương tác và bình luận; query tests kiểm tra SQL PostgreSQL và không đọc các cột Identity riêng tư.

Để chạy các bài PostgreSQL, dùng database kiểm thử riêng theo hướng dẫn trong [RECIPE_SEEDING.md](RECIPE_SEEDING.md), với `COOKSHARE_RUN_POSTGRES_TESTS=1` và `COOKSHARE_TEST_CONNECTION`. Tính năng này dùng các bảng có sẵn, không thêm migration và không tự tạo Follow hay tương tác mẫu.
