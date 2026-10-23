# Tích hợp giao diện công thức với database

> Tài liệu này ghi nhận trạng thái khi hoàn tất tích hợp Recipe. Issue #76 và #94 sau đó bổ sung Likes/Comments và Favorites dùng database trên các trang công thức thật. Toàn bộ phần việc được tích hợp trên nhánh `feature/persistent-favorites`; xem [PERSISTENT_FAVORITES.md](PERSISTENT_FAVORITES.md) cho phạm vi review và kết quả tổng hợp mới nhất. Các mô tả Saved legacy và số liệu 183 test bên dưới thuộc giai đoạn triển khai trước đó; dữ liệu browser cũ được giữ nguyên nhưng không dùng để render Saved database hiện tại.

## Luồng dữ liệu

- Create/Edit gửi biểu mẫu MVC có anti-forgery token; Identity cung cấp ID tác giả phía server. `SaveChangesAsync` dùng kết nối PostgreSQL hiện có trong `ApplicationDbContext`.
- Home, Explore, Details và Profile đọc công thức từ EF Core, dùng ID thật và cùng partial `_RecipeCard`. Latest lấy 3 công thức theo `CreatedAt DESC, Id DESC`; Featured lấy 3 ID đầu tiên theo thứ tự tăng dần, là lựa chọn ổn định tạm thời.
- Explore lọc `q` theo tên và `category` phía server, kết hợp được hai bộ lọc. Không phân biệt hoa/thường; không bổ sung tìm kiếm bỏ dấu.
- Edit giữ nguyên ID, tác giả và ngày tạo. Edit/Delete kiểm tra quyền sở hữu trên server. Delete dùng POST và giữ nguyên quy tắc khóa ngoại hiện có.
- Biểu mẫu giữ dữ liệu khi validation hoặc lưu thất bại. Ảnh hỗ trợ URL HTTP/HTTPS và đường dẫn nội bộ; Base64 bị từ chối. Ảnh thiếu/không hợp lệ dùng placeholder, ảnh tải lỗi dùng fallback giao diện hiện có.

Không thay đổi schema, migration, cấu hình kết nối hoặc credentials. Không chạy migration, seed hay thao tác ghi trên Neon dùng chung trong lần triển khai này.

## Dữ liệu trình duyệt cũ và phạm vi tương tác

`cookshare_recipes` được giữ nguyên. Chỉ trang Saved cũ đọc bộ sưu tập này; không tự tạo dữ liệu mẫu, nhập vào database, sửa hay xóa công thức localStorage. Nội dung đã lưu có thể mở ngay trong thẻ, không dùng ID local để mở Details/Edit của database.

Likes, Saved và Ratings cũ vẫn hoạt động trong bộ sưu tập trình duyệt. Comments và Follow/Unfollow chưa được tích hợp database trong công việc này. Các script legacy không được gắn vào thẻ công thức đã xuất bản để tránh ghép tương tác demo với công thức thật hoặc hiển thị số liệu sai. Profile vẫn giữ dữ liệu follows/favorites phía database đã có từ trước.

Việc chuyển các tương tác này và công thức localStorage cũ vào database cần công việc riêng, có ánh xạ danh tính/ID và quyết định của người dùng.

## Kiểm chứng đã chạy

- `dotnet restore CookShare.sln`: đạt.
- `dotnet build CookShare.sln`: đạt; lần biên dịch đầy đủ còn các cảnh báo sẵn có ở DbContext/migrations và cấu hình documentation.
- `dotnet test CookShare.sln`: **183 đạt, 0 thất bại, 10 bỏ qua**, tổng 193.
- Trong đó 42 ca HTTP biệt lập kiểm tra routing, Razor, EF InMemory, ID sinh tự động, tác giả, Create/Edit/Delete trên nhiều trang, tìm kiếm, validation, authorization, anti-forgery và ảnh fallback.
- Kiểm tra dịch bộ lọc sang SQL của Npgsql bằng `ToQueryString`: đạt, kết nối không được mở.
- Kiểm tra style/analyzers, whitespace các tệp C# đã thay đổi và `git diff --check`: đạt.
- Whitespace toàn solution: chưa đạt do 23 tệp ngoài phạm vi dùng CRLF/định dạng sẵn có; không sửa hàng loạt các tệp này.

Kết quả test ở `CookShare.Tests/TestResults/recipe-integration.trx`; log kiểm chứng ở `artifacts/recipe-integration/`. Các thư mục này được Git bỏ qua.

## Chạy kiểm thử PostgreSQL riêng

10 test PostgreSQL hiện có được giữ nguyên assertions và chỉ chạy khi đồng thời có `COOKSHARE_RUN_POSTGRES_TESTS=1` và `COOKSHARE_TEST_CONNECTION`. Factory cũ tự chạy migration và ghi dữ liệu: chỉ bật với database kiểm thử riêng được phép sử dụng, không dùng database Neon chia sẻ. Không ghi connection string vào tài liệu, terminal hoặc Git.

Suite HTTP mặc định thay hoàn toàn provider bằng EF InMemory, dùng database riêng cho mỗi ca và Data Protection tạm thời. Suite này không xác nhận khóa ngoại PostgreSQL, lưu bền vững qua lần khởi động lại, hay khả năng kết nối Neon thực tế.

## Kiểm tra thủ công còn lại

Khi có database phát triển an toàn và hai tài khoản thử nghiệm:

1. User A đăng nhập, tạo công thức; kiểm tra Details, Latest, Explore và Profile cùng ID/tác giả/nội dung.
2. Refresh và khởi động lại ứng dụng; xác nhận dữ liệu còn nguyên.
3. Sửa công thức; kiểm tra nội dung trên các trang và ID/ngày tạo không thay đổi.
4. User B xem công thức; kiểm tra truy cập trực tiếp Edit và POST Edit/Delete bị từ chối.
5. User A xóa bằng biểu mẫu; kiểm tra danh sách và Profile cập nhật, Details trả 404.
6. Kiểm tra desktop/mobile, trạng thái trống, ảnh lỗi, validation và thông báo thành công/lưu thất bại.

Chưa xác minh toàn bộ luồng trên Neon thật hoặc browser desktop/mobile của ứng dụng.

Kiểm tra JavaScript bằng Chrome headless chưa thực thi được: Chrome lỗi khởi tạo GPU/quyền truy cập trong sandbox; yêu cầu chạy ngoài sandbox đã bị hủy. Không coi đây là kiểm thử browser đã đạt.

## Pull Request đề xuất

Title: `feat: integrate recipe frontend with Neon PostgreSQL`

Base: `develop`; compare: `feature/recipe-db-ui-integration`.

Description:

> Home/Explore/Details/Profile dùng chung dữ liệu công thức EF Core và thẻ Razor; thêm tìm kiếm/lọc phía server, liên kết tác giả thật, validation ảnh và xử lý lỗi lưu. Giữ bảo vệ quyền sở hữu/CSRF của CRUD. Tách bộ sưu tập localStorage cũ để tránh xung đột ID. Thêm 42 kiểm thử HTTP biệt lập và các kiểm thử controller/validation/SQL translation; 183 đạt, 10 PostgreSQL cần opt-in. Các tương tác legacy vẫn chỉ lưu trên trình duyệt; chưa kiểm chứng Neon và restart thực tế. Refs #75

Commit đề xuất: `feat: integrate recipe pages with database`. Chưa commit, push hoặc tạo PR.
