# Creator Profile — Issue #117

Profile có ảnh bìa, Bio, ngày tham gia, thống kê, các tab nội dung và tối đa 3 công thức nổi bật. Xem [ảnh trước/sau và các màn hình kiểm thử](ui/issue117/README.md).

## Chạy và cập nhật database

Sau khi lấy nhánh `feature/enhanced-creator-profile`, áp dụng migration trước khi mở website, bằng connection Development đã cấu hình trong User Secrets:

```powershell
dotnet ef database update --project CookShare/CookShare.csproj -- --environment Development
dotnet run --project CookShare/CookShare.csproj
```

Migration `20261023234416_AddCreatorProfile` thêm hai cột nullable `AspNetUsers.Bio` (300 ký tự), `CoverImageUrl` (500 ký tự) và bảng `ProfileFeaturedRecipes`. Migration không điền Bio, ảnh bìa hay ghim công thức cho tài khoản hiện có. Chạy migration riêng trước khi triển khai phiên bản ứng dụng mới; ứng dụng không tự migrate khi khởi động.

## Kiểm thử bằng giao diện

1. Đăng nhập, mở **Căn bếp cá nhân → Chỉnh sửa hồ sơ** (`/Profile/Edit`). Nhập tên hiển thị, Bio tiếng Việt có xuống dòng và ảnh bìa `/img/recipes/kitchen.webp`. Avatar và ảnh bìa có preview; Bio có bộ đếm tối đa 300 ký tự.
2. Chọn **Lưu thay đổi**, tải lại Profile rồi mở lại Edit. Bio và ảnh phải giữ nguyên. **Hủy** quay về Profile và không lưu thay đổi đang nhập.
3. Thử URL không hợp lệ, Bio dài quá 300 ký tự hoặc chứa thẻ HTML. Form hiển thị lỗi và giữ nội dung nhập. Thử đường dẫn `/missing-cover.webp`: preview báo không tải được; Profile dùng nền dự phòng nếu lưu đường dẫn này.
4. Chọn **Quản lý công thức nổi bật** (`/Profile/Highlights`). Chọn từ các công thức chính mình đã đăng vào 3 vị trí, dùng nút lên/xuống để đổi thứ tự rồi lưu. Profile phải hiển thị đúng thứ tự, ảnh, danh mục và đánh giá thật. Chọn **Không ghim** để bỏ một vị trí; thao tác không sửa hoặc xóa công thức gốc.
5. Mở Profile bằng tài khoản khác hoặc cửa sổ ẩn danh: xem được Bio, ảnh bìa, các công thức nổi bật; các nút chỉnh sửa/quản lý và tab **Đã lưu** chỉ có cho chủ tài khoản. Follow/Unfollow, Like, Save, Recipe Details và CRUD tiếp tục dùng luồng hiện có.
6. Kiểm tra các tab bằng chuột và bàn phím: Tab để vào danh sách tab, mũi tên trái/phải hoặc Home/End để chuyển, Enter/Space để kích hoạt. Kiểm tra ở 360px, 768px, 1440px và bật `prefers-reduced-motion` trong DevTools.

Tài khoản chưa có công thức cần đăng công thức thật trước khi chọn ghim. Profile chưa có Bio/ảnh bìa dùng trạng thái mặc định; khách xem không thấy thẻ nổi bật giả.

## Validation, quyền truy cập và lưu trữ

- Bio là văn bản thuần, được Razor encode khi hiển thị và dùng `textContent` khi preview. Chuẩn hóa CRLF thành LF trước khi kiểm tra giới hạn 300 ký tự; giữ xuống dòng bằng CSS. Không nhận thẻ HTML hoặc ký tự điều khiển ngoài xuống dòng/tab.
- Avatar và Cover dùng cơ chế `RecipeImageReference` hiện có, giới hạn 500 ký tự và chỉ cho phép đường dẫn nội bộ hoặc HTTPS. Không nhận `http:`, `javascript:`, `data:`, URL dạng `//host`, traversal hay ký tự không an toàn. Trình duyệt tải ảnh; server không tải nội dung từ URL này. Không thêm hệ thống upload.
- Edit và Highlights yêu cầu đăng nhập, POST có anti-forgery. User ID lấy từ claims hiện tại; form không cho đổi Username, Email, Password hoặc chọn tài khoản khác. Favorites chỉ được truy vấn và render cho chủ tài khoản.
- Backend kiểm tra tối đa 3 công thức, không trùng, còn tồn tại và thuộc chủ tài khoản. Khóa chính `(UserId, RecipeId)` chống trùng; unique index `(UserId, DisplayOrder)` cùng check `DisplayOrder BETWEEN 1 AND 3` bảo đảm tối đa 3 vị trí trong PostgreSQL.
- Thay toàn bộ danh sách trong một transaction. Khóa hàng của chính người dùng bằng `FOR UPDATE` để hai lượt đổi thứ tự đồng thời không tạo danh sách trộn. Xóa ghim cũ và thêm ghim mới cùng transaction; lỗi ghi rollback và giữ danh sách trước đó. Xóa Recipe hoặc User cascade dữ liệu ghim liên quan.
- Preview ảnh xử lý tải chậm, URL lỗi và phản hồi cũ; lỗi ảnh chuyển sang fallback. Tab và form vẫn có liên kết/native submit khi JavaScript không chạy. Animation hỗ trợ reduced motion.

## Kiểm thử tự động

```powershell
dotnet build CookShare.sln --configuration Release
dotnet test CookShare.sln --configuration Release --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-CreatorProfileBrowserTests.ps1 -Viewport Desktop
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-CreatorProfileBrowserTests.ps1 -Viewport Mobile
```

`CreatorProfileValidationTests` kiểm tra Bio/URL; `CreatorProfileIntegrationTests` kiểm tra form, xác thực, quyền sở hữu, anti-forgery, preview lỗi, thứ tự ghim và rating thật. Browser harness thực thi CSS/JS production với 32 kiểm tra mỗi viewport; không cần database hay npm.

Các bài PostgreSQL được bật riêng trên database kiểm thử có thể xóa dữ liệu, tuyệt đối không trỏ vào Development đang dùng hoặc Production:

```powershell
$env:COOKSHARE_RUN_POSTGRES_TESTS = '1'
$env:COOKSHARE_TEST_CONNECTION = '<connection đến PostgreSQL kiểm thử riêng>'
dotnet test CookShare.sln --configuration Release --no-build
```

`CreatorProfilePostgresTests` kiểm tra lưu Bio/Cover, cạnh tranh đổi thứ tự, rollback lỗi insert, ràng buộc vị trí và cascade. Bài migration tạo database ngẫu nhiên `cookshare_test_creator_*`, áp dụng schema cũ, thêm hồ sơ cũ rồi migrate và kiểm tra dữ liệu được giữ nguyên; sau đó xóa đúng database tạm này. Vì vậy user PostgreSQL kiểm thử cần quyền tạo database. Các suite khác có thể reset database được cấu hình, nên luôn dùng database kiểm thử riêng.

Ảnh ví dụ đầy đủ Bio/Cover/Highlights được chụp từ fixture trong PostgreSQL kiểm thử riêng. Không tự ghi đè hoặc thêm dữ liệu Profile vào database ứng dụng.
