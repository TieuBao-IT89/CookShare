# Khẩu phần, thời gian và độ khó

## Khẩu phần

`Recipes.Servings` lưu khẩu phần gốc từ 1 đến 20, mặc định 4. Migration `AddRecipeServings` gán 4 cho bản ghi cũ; tác giả nên kiểm tra lại khi sửa công thức cũ.

Trang chi tiết tính lượng nguyên liệu theo tỷ lệ khẩu phần mới/gốc. Việc này không ghi lại database. Trở về khẩu phần gốc sẽ khôi phục đúng văn bản ban đầu.

Bộ nhận diện hỗ trợ số ở đầu dòng, số thập phân, phân số và một số ký tự như `½`. Ví dụ `200 g gạo` của 4 phần thành `400 g gạo` khi chọn 8 phần. Dòng không nhận diện được như `muối vừa đủ` giữ nguyên. Bộ nhận diện không hỗ trợ mọi cách viết định lượng hoặc chuyển đổi đơn vị.

## Thời gian và độ khó

| Trường | Giá trị |
| --- | --- |
| `PreparationTimeMinutes` | Nullable; 1–1440 phút nếu có |
| `CookingTimeMinutes` | Nullable; 1–1440 phút nếu có |
| `Difficulty` | Nullable; `Easy`, `Medium`, `Hard`, hiển thị Dễ/Trung bình/Khó |

Form tạo/sửa cho phép để trống. Migration `AddRecipeCookingMetadata` giữ `NULL` cho dữ liệu cũ, không suy đoán metadata. Thẻ công thức, trang chi tiết và feed hiển thị thông tin đã có.

Khám phá lọc theo độ khó và tổng thời gian tối đa. Tổng chỉ tính khi có đủ cả hai thời gian; công thức thiếu một trong hai bị loại khi bật lọc thời gian. Query không hợp lệ trả HTTP 400; `maxTotalMinutes` hợp lệ từ 1 đến 2880.

Feed rút gọn mô tả dài; “Đọc thêm câu chuyện” mở phần còn lại ngay trong nội dung hiện tại.

## Migration và kiểm tra

Áp dụng vào database Development đã cấu hình:

```bash
dotnet ef database update --project CookShare/CookShare.csproj -- --environment Development
```

`AddRecipeServings` chạy trước `AddRecipeCookingMetadata`. Snapshot và mô hình đích migration sau cùng chứa cả khẩu phần lẫn metadata. Xem [Database design](DATABASE_DESIGN.md) và [Testing](TESTING.md).

Kiểm tra tăng/giảm khẩu phần, khôi phục văn bản, nguyên liệu không định lượng, create/edit giữ đủ metadata và ảnh, lọc dữ liệu thiếu/đủ, mở rộng feed và desktop/mobile.
