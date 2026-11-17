# Thành phẩm đã nấu thử và kiểm duyệt

Người đã đăng nhập chọn **Chia sẻ thành phẩm** trên Bảng tin, trang công thức hoặc cuối chế độ nấu. Chọn công thức gốc còn hiển thị, tải một ảnh và viết ghi chú tối đa 1000 ký tự. Ảnh và ghi chú là nội dung công khai. Người đăng có thể sửa hoặc xóa bài; công thức gốc được giữ nguyên.

Bài xuất hiện trên Bảng tin mới nhất, Bảng tin theo dõi, mục Đã nấu trong hồ sơ và thư viện thành phẩm ở công thức gốc. Bài chỉ công khai khi cả bài và công thức đều còn tồn tại, không bị ẩn. Tác giả công thức nhận thông báo có người nấu thử; không tự thông báo cho chính mình.

## Các route

| Route | Quyền và hành vi |
| --- | --- |
| `GET /CookedPosts/Create?recipeId=<id>` | Đăng nhập; form thành phẩm cho công thức được chọn |
| `GET /CookedPosts/Create?query=<tên-món>` | Đăng nhập; chọn trong tối đa 20 công thức phù hợp |
| `POST /CookedPosts/Create` | Đăng nhập + CSRF; tạo bài với khóa gửi duy nhất |
| `GET /CookedPosts/Details/<id>` | Công khai khi bài và công thức hiển thị |
| `GET/POST /CookedPosts/Edit/<id>` | Chỉ chủ bài; POST cần CSRF; không đổi công thức gốc |
| `POST /CookedPosts/Delete/<id>` | Chỉ chủ bài + CSRF; gọi lại sau khi đã xóa vẫn an toàn |
| `GET /media/cooked-posts/<id>/<fileName>` | Kiểm tra bài, tên ảnh và công thức ở mỗi lần đọc; trả 404 khi ẩn hoặc xóa |
| `GET/POST /ContentReports/Create` | Đăng nhập; loại đích `Recipe` hoặc `CookedPost`; POST cần CSRF |
| `GET /Moderation` | Chỉ role Identity `Moderator`; hàng chờ, 25 báo cáo/trang |
| `GET /Moderation/Details/<id>` | Chỉ moderator; nội dung, lý do và nhật ký xử lý |
| `POST /Moderation/Decide/<id>` | Chỉ moderator + CSRF; `Dismiss`, `Hide`, `Restore` và lý do 3–1000 ký tự |
| `GET /Moderation/CookedPostImage/<id>` | Chỉ moderator; xem ảnh để xử lý báo cáo, kể cả khi đang ẩn |
| `GET /Moderation/RecipeImage/<id>` | Chỉ moderator; xem ảnh upload công thức để xử lý báo cáo |

Các route media trả `Cache-Control: no-store` và `X-Content-Type-Options: nosniff`. URL mới của thành phẩm không dùng route ảnh công thức. Xem [ảnh công thức](RECIPE_IMAGES.md) để phân biệt hai kho lưu trữ.

## Báo cáo và xử lý

Lý do gồm spam, quấy rối, nội dung không an toàn, quyền sử dụng nội dung và lý do khác. Chi tiết tối đa 1000 ký tự; chỉ moderator thấy báo cáo. Một tài khoản gửi tối đa một báo cáo cho mỗi nội dung; gửi lại không sinh thêm dòng.

- **Bỏ qua** đóng báo cáo và giữ trạng thái hiển thị hiện tại.
- **Ẩn** chặn nội dung và media trên các luồng công khai; giữ dữ liệu để xem xét và khôi phục.
- **Khôi phục** mở lại nội dung. Thành phẩm vẫn bị chặn nếu công thức gốc còn ẩn.

Thao tác, trạng thái báo cáo và thay đổi hiển thị được lưu cùng giao dịch. Nhật ký giữ người xử lý, thời gian UTC và lý do. Gửi lại cùng quyết định không tạo thêm nhật ký khi trạng thái đã khớp. Màn hình quản trị dùng route media riêng theo role, không mở lại media cho khách.

Moderator được cấp từ môi trường quản trị đáng tin cậy, không có form tự đăng ký role:

```bash
dotnet run --project CookShare -- moderator grant <existing-username>
dotnet run --project CookShare -- moderator revoke <existing-username>
```

Chạy trên đúng database quản trị đã cấu hình. Người được thay đổi quyền cần đăng xuất rồi đăng nhập lại để cập nhật role trong cookie. Không cấp quyền từ query string, trường form, header trình duyệt hoặc giá trị do client tự khai.

## Upload và storage

`LocalSocialImageStorage` tái sử dụng bộ kiểm tra upload của `LocalRecipeImageStorage`: JPG/JPEG, PNG, WebP; tối đa 5 MiB theo độ dài thực; tên GUID do server sinh. Bổ sung kiểm tra kích thước trong header: mỗi chiều tối đa 8192 px, tổng tối đa 40 triệu điểm ảnh. Đây là kiểm tra định dạng/header, không phải giải mã và re-encode toàn bộ ảnh hoặc quét malware.

| Môi trường | Thư mục mặc định |
| --- | --- |
| Local | `<content-root>/App_Data/cooked-post-images` |
| Azure App Service có `HOME` và `WEBSITE_INSTANCE_ID` | `<HOME>/data/CookShare/cooked-post-images` |

`SocialImages__StoragePath` ghi đè mặc định; đường dẫn tương đối tính từ content root. Thư mục nằm trong `wwwroot` bị từ chối. Dùng thư mục riêng với `RecipeImages__StoragePath`; tài khoản chạy ứng dụng cần quyền đọc/ghi. Kho mặc định được bỏ qua bởi Git và publish. Trên nhiều instance, dùng chung volume bền vững; sao lưu database và media cùng nhau.

Tạo bài và thông báo chạy cùng giao dịch database. Unique index `(UserId, SubmissionKey)` ngăn tạo bài trùng khi gửi lại. `Notification.EventKey` chống thông báo trùng. Nếu lưu database thất bại, xóa ảnh vừa upload. Khi thay ảnh hoặc xóa bài, xóa ảnh cũ sau khi lưu database thành công. Khi xóa công thức hoặc tài khoản, các bài liên quan bị xóa và media không còn đọc công khai được; file còn dư có thể dọn bằng lệnh dưới.

Nếu tiến trình dừng giữa upload và commit hoặc storage tạm thời không xóa được file, chạy:

```bash
dotnet run --project CookShare -- cleanup-social-media
```

Lệnh đối chiếu tham chiếu với database theo lô 200 tệp, kể cả bài đang ẩn. Chỉ xóa file không còn tham chiếu và cũ hơn 24 giờ; file mới được giữ để tránh xóa upload đang xử lý. Xác minh đúng connection string và storage trước khi chạy. Lệnh không thay đổi bài hoặc thông báo.

Upload và báo cáo có giới hạn riêng 20 yêu cầu/5 phút cho mỗi danh tính đã xác thực trong mỗi tiến trình ứng dụng; vượt giới hạn trả 429. Form upload giới hạn request 6 MiB để chứa multipart và dữ liệu văn bản; file ảnh vẫn tối đa 5 MiB. Nếu triển khai nhiều instance, cấu hình thêm giới hạn ở reverse proxy khi cần giới hạn dùng chung.

## Chính sách xóa và nhật ký

Xóa bài thành phẩm xóa dữ liệu bài và thu hồi media. Thông báo cũ không trỏ tới nội dung đã xóa; quan hệ bài trong thông báo dùng `SET NULL`. Ẩn giữ dữ liệu, tham chiếu ảnh và audit để khôi phục. Xóa công thức xóa bài thành phẩm liên quan theo quan hệ cascade.

Báo cáo giữ định danh loại/ID nội dung có chủ đích, không có khóa ngoại tới nội dung có thể xóa. Nhật ký vì vậy vẫn giải thích được việc xử lý sau khi bài hoặc công thức bị xóa. Xóa tài khoản người báo cáo hoặc moderator đặt tham chiếu danh tính thành `NULL`; giao diện hiển thị “Tài khoản đã xóa”. Chỉ có thể bỏ qua báo cáo khi nội dung không còn tồn tại.

## Kiểm chứng

```bash
dotnet test CookShare.sln --filter "FullyQualifiedName~CookedPostServiceTests|FullyQualifiedName~LocalSocialImageStorageTests|FullyQualifiedName~SocialContentControllerTests|FullyQualifiedName~SocialModerationIntegrationTests|FullyQualifiedName~SocialVisibilityLifecycleIntegrationTests"
```

Các test SQLite chạy không cần PostgreSQL: rollback bài/thông báo/media, gửi lặp, quyền sửa/xóa, file thay thế sau commit, dọn ảnh mồ côi, media khi ẩn bài/công thức, nhật ký nguyên tử và xóa đích. Test HTTP dùng cookie Identity thật và role cấp qua `RoleManager`: từ chối người dùng thường ở middleware, từ chối POST thiếu CSRF, moderator ẩn/khôi phục và chặn URL ảnh trực tiếp. Test vòng đời kiểm tra nội dung ẩn trên Bảng tin, Khám phá, hồ sơ, bộ sưu tập, URL ảnh cũ và mới; xóa tài khoản giữ dữ liệu của người khác và lịch sử xử lý. Test storage dùng ảnh PNG/JPEG/WebP, tên tệp không tin cậy, header giả, giới hạn kích thước và từ chối storage trong web root.

Chạy thêm PostgreSQL và browser harness theo [Testing](TESTING.md). SQLite không thay thế kiểm chứng unique constraint/giao dịch trên PostgreSQL hoặc tương tác trình duyệt thực tế.
