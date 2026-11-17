# Bộ sưu tập công thức

Bộ sưu tập giúp một người dùng sắp xếp cùng một công thức vào nhiều nhóm. Dữ liệu lưu trong PostgreSQL theo tài khoản Identity; không dùng localStorage.

## Quyền riêng tư và trạng thái lưu

- Bộ mới luôn **riêng tư**, kể cả khi browser gửi `IsPublic=true` lúc tạo. Chủ bộ bật công khai trong trang chỉnh sửa.
- Chỉ chủ bộ tạo, đổi tên, đổi quyền hiển thị, xóa hoặc thay đổi thành viên. Tài khoản khác nhận 404 khi truy cập bộ riêng hoặc gọi mutation bằng ID của bộ đó; bộ công khai cũng chỉ cho người khác đọc.
- Hồ sơ công khai dùng projection `RecipeCollectionService.PublicForOwnerAsync`, luôn lọc `IsPublic`. Hộp chọn chỉ liệt kê bộ của người đang đăng nhập. Nội dung cá nhân trả `Cache-Control: no-store`.
- **Đã lưu** (`Favorite`) và **Trong bộ sưu tập** (`CollectionRecipe`) độc lập. Thêm vào bộ không tự tạo Favorite. Bỏ lưu không gỡ khỏi bộ. Gỡ khỏi một bộ hoặc xóa bộ không ảnh hưởng bộ khác hay công thức gốc.
- Công thức bị ẩn không xuất hiện trong danh sách, số lượng hoặc ảnh bìa của bộ; không thể thêm công thức bị ẩn qua URL trực tiếp. Ảnh bìa lấy từ công thức đang hiển thị và dùng fallback khi thiếu ảnh.

## Route và biểu mẫu

| Route | Hành vi |
| --- | --- |
| `GET /Collections` | Các bộ của chủ tài khoản; 24 bộ mỗi trang |
| `GET /Collections/{id}` | Đọc bộ công khai hoặc bộ riêng của chính mình; 24 món mỗi trang |
| `GET /Collections/Create` và `POST /Collections/Create` | Tạo bộ riêng; `RecipeId` tùy chọn tạo luôn thành viên trong cùng một lần lưu |
| `GET /Collections/{id}/Edit` và `POST /Collections/{id}/Edit` | Đổi tên, mô tả và quyền công khai |
| `POST /Collections/{id}/Delete` | Xóa bộ và các quan hệ thành viên |
| `GET /Recipe/{recipeId}/Collections` | Chọn nhiều bộ, tạo bộ mới rồi thêm món, thao tác Favorite riêng |
| `POST /Collections/{id}/Recipes/{recipeId}/Add` | Thêm thành viên, lặp lại an toàn |
| `POST /Collections/{id}/Recipes/{recipeId}/Remove` | Gỡ riêng thành viên, lặp lại an toàn |

POST yêu cầu đăng nhập và anti-forgery token. OwnerId lấy từ claim đã xác thực; ID của bộ/công thức lấy từ route, bỏ qua ID giả trong body. Tên tối đa 80 ký tự, mô tả tối đa 300 ký tự; Razor encode nội dung người dùng.

Giữ nguyên endpoint Favorite hiện có. Khi có `returnUrl` cục bộ và request thông thường, Save/Unsave chuyển hướng về trang sau khi ghi thành công. Request AJAX hoặc request không có `returnUrl` vẫn trả JSON như trước. URL ngoài ứng dụng không được chuyển hướng.

## Giao diện và dự phòng

Liên kết **Bộ sưu tập** ở thẻ món/chi tiết mở trang chọn khi JavaScript tắt. Khi có JavaScript, liên kết mở `<dialog>` với vùng loading/error, Escape và trả focus về liên kết ban đầu. Các form POST giữ CSRF và khóa nút khi gửi; lỗi giữ nguyên trạng thái để thử lại. Hộp đóng khi tab bị ẩn để lần mở sau đọc lại quyền/trạng thái từ server.

Trang **Đã lưu** có hai mục rõ ràng: **Tất cả món đã lưu** và **Bộ sưu tập**. Các bộ có phân trang; hộp chọn có liên kết quản lý và chuyển trang, không tải toàn bộ bộ/món vào browser.

## Schema và vòng đời

`RecipeCollections` có OwnerId, Name, Description, IsPublic mặc định false, CreatedAt và UpdatedAt. `CollectionRecipes` dùng khóa chính kép `PK_CollectionRecipes` theo `(CollectionId, RecipeId)` và chỉ mục phục vụ thứ tự `AddedAt`, `RecipeId`. Không thể có hai thành viên trùng cặp kể cả khi gửi đồng thời; chỉ lỗi trùng đúng khóa này được coi là thành công lặp.

Xóa bộ cascade thành viên. Xóa công thức cascade thành viên ở mọi bộ; không xóa bộ. Khôi phục công thức từng bị ẩn làm nó hiển thị trở lại trong các bộ còn giữ quan hệ. Xóa tài khoản xóa các bộ thuộc tài khoản và các quan hệ trỏ tới công thức sẽ bị xóa trước khi xóa Identity user. Bộ sưu tập không sao chép ảnh nên không tạo thêm media mồ côi.

## Kiểm thử

`RecipeCollectionsIntegrationTests` dùng HTTP host InMemory cô lập để kiểm tra bộ riêng/public, hai tài khoản, CSRF, giả ID/OwnerId, lưu nhiều bộ, Favorite độc lập, tạo bộ kèm món, reload, ẩn công thức và encode tên. `RecipeCollectionServiceTests` kiểm tra xử lý cạnh tranh, projection công khai và cascade trên SQLite. `RecipeCollectionsPostgresTests` kiểm tra unique constraint với hai request đồng thời, projection dịch sang SQL thật, quyền riêng tư và cascade trên PostgreSQL riêng.

```bash
dotnet test CookShare.sln --configuration Release --filter FullyQualifiedName~RecipeCollection
```

Kiểm thử PostgreSQL dùng database riêng theo [TESTING.md](TESTING.md). Không bật test ghi trên database ứng dụng hoặc Neon dùng chung. Kết quả thực thi và ảnh viewport được ghi trong báo cáo CookShare Social 2.0; các test HTTP InMemory không thay thế xác minh constraint/transaction trên PostgreSQL.
