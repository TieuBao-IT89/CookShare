# Ảnh công thức

Chọn ảnh trên form tạo/sửa để xem trước. Upload diễn ra khi gửi form; không chọn tệp mới khi sửa sẽ giữ ảnh đang lưu.

## Giới hạn

- JPG/JPEG, PNG và WebP, tối đa 5 MiB (5 × 1024 × 1024 byte).
- Server kiểm tra phần mở rộng, độ dài thực và dấu hiệu định dạng. Đây không phải giải mã/re-encode toàn bộ ảnh hoặc quét malware.
- Server sinh tên GUID; tên từ trình duyệt không chọn đường dẫn lưu.
- File ở ngoài `wwwroot`; PostgreSQL chỉ lưu `/uploads/recipes/<fileName>` trong `Recipes.Image`.
- Route ảnh cho phép đọc công khai theo tên hợp lệ, trả Content-Type, `nosniff` và cache immutable. Không dùng ảnh công thức để lưu tài liệu riêng tư.

Mã chính: [LocalRecipeImageStorage](../CookShare/Services/LocalRecipeImageStorage.cs), [RecipeImagesController](../CookShare/Controllers/RecipeImagesController.cs), [RecipeController](../CookShare/Controllers/RecipeController.cs).

## Cấu hình storage

| Môi trường | Đường dẫn mặc định |
| --- | --- |
| Local | `<content-root>/App_Data/recipe-images` |
| Azure App Service, có `WEBSITE_INSTANCE_ID` và `HOME` | `<HOME>/data/CookShare/recipe-images` |

`RecipeImages__StoragePath` ghi đè mặc định. Đường dẫn tương đối tính từ content root; đường dẫn tuyệt đối được hỗ trợ. Storage trong web root bị từ chối.

```powershell
$env:RecipeImages__StoragePath = "E:/CookShareData/recipe-images"
dotnet run --project CookShare/CookShare.csproj --launch-profile http
```

Tài khoản chạy ứng dụng cần quyền đọc/ghi. Thư mục mặc định được bỏ qua bởi Git và loại khỏi publish.

## Vận hành

Dùng volume bền vững; trên Azure kiểm tra cấu hình storage của App Service. Khi có nhiều instance, mọi instance phải dùng cùng storage; các thư mục local độc lập khiến ảnh lúc có lúc mất.

Sao lưu cả database và thư mục ảnh. Khôi phục database đơn lẻ không khôi phục file. Nếu đổi `StoragePath`, di chuyển nội dung và xác minh đọc ảnh trước khi chuyển lưu lượng.

Upload mới được xóa khi lưu database thất bại. Hiện chưa tự dọn ảnh cũ khi thay ảnh, xóa công thức hoặc xóa tài khoản. Trước khi dọn file cần đối chiếu tham chiếu database và chính sách sao lưu.

## Kiểm tra

```bash
dotnet test CookShare.sln --filter FullyQualifiedName~RecipeImageUploadIntegrationTests
```

Bộ test dùng storage tạm và SQLite riêng: định dạng/kích thước, tên không tin cậy, quyền sở hữu, CSRF, rollback file và kết hợp upload với khẩu phần/thời gian/độ khó. Kiểm tra thêm ảnh sau restart/deploy trên storage đích.
