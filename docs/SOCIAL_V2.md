# CookShare Social 2.0

Triển khai trên nhánh `feature/cookshare-social-v2`, xuất phát từ `develop` tại `2d125b4`. Lần kiểm tra PR/Issue lúc bắt đầu không có PR mở; Issue #49 liên quan tìm kiếm. Các phần nháp công thức, nguyên liệu/bước có cấu trúc, tủ lạnh, thử thách tuần và realtime nằm ngoài lần triển khai này.

## Giai đoạn 1: trải nghiệm và khám phá

- Giữ Razor, CSS/JavaScript thuần, Identity và các route công khai cũ. Thanh dưới mobile có Bảng tin / Khám phá / Đăng / Đã lưu / Hồ sơ; thông báo ở top bar, menu có trạng thái active và lối vào kiểm duyệt khi có role.
- Composer phân biệt Công thức mới và Đã nấu thử. Feed giữ ba cột trên desktop, một cột trên điện thoại; ảnh có kích thước/tỷ lệ ổn định và fallback.
- Khám phá có bộ lọc dạng sheet trên mobile, chip xóa riêng và form GET hoạt động khi tắt JavaScript. Chi tiết đưa thao tác nấu lên đầu, giữ scaler, checklist, bình luận/đánh giá và cooking mode.
- Font/icon hiện có được phục vụ local, không đổi nhận diện Warm & Fresh. Xem [nguồn và giấy phép](THIRD_PARTY_ASSETS.md), [audit tám nguồn tham khảo](SOCIAL_V2_UI_AUDIT.md) và [ảnh kiểm chứng](ui/social-v2/README.md).

### Tìm kiếm

`GET /Recipe` nhận `q`, `ingredient`, `author`, `category`, `difficulty`, `maxTotalMinutes`, `sort` và `cursor`. `q` tìm trong tên/mô tả, nguyên liệu tìm trong văn bản nguyên liệu, tác giả chỉ tìm display name/username công khai. Các tham số và bộ lọc được giữ trong URL; bỏ một chip sẽ bỏ cursor cũ.

Tìm kiếm chuẩn hóa Unicode, bỏ dấu tiếng Việt và ánh xạ `đ/Đ` sang `d`; ký tự `%`, `_`, dấu nháy được tìm như văn bản, không ghép SQL. Các cột chuẩn hóa cập nhật khi EF lưu công thức/hồ sơ. Migration backfill nội dung cũ bằng biểu thức tương đương ở PostgreSQL. Không sửa tên, mô tả hoặc văn bản nguyên liệu gốc.

Mỗi trang tối đa 24 thẻ, dùng projection không tải hướng dẫn nấu hoặc trường Identity bí mật. `newest` sắp theo `CreatedAt DESC, Id DESC`; `popular` dùng điểm `2 × likes + comments + tổng số sao`, rồi thời gian và ID. Cursor chứa điểm/thời gian/ID, mốc thời gian trang đầu và dấu nhận diện bộ lọc. Bài mới sau mốc không chen vào trang sau; thay đổi điểm tương tác được phản ánh khi truy vấn lại, nên tải lại đầu danh sách để nhận thứ tự mới nhất. Tổng kết quả được đếm riêng, không tải toàn bộ entity để phân trang.

Chỉ mục GIN `pg_trgm` hỗ trợ tìm chuỗi trên tên/mô tả, nguyên liệu và tên tác giả đã chuẩn hóa. Cần PostgreSQL có `normalize` và extension `pg_trgm`; CI dùng PostgreSQL 16. Tài khoản chạy migration cần quyền cài trusted extension trong database; tài khoản runtime chỉ cần quyền dữ liệu thông thường.

## Giai đoạn 2: bộ sưu tập

[Collections](COLLECTIONS.md) mô tả CRUD, quyền đọc và các route. Bộ mới luôn riêng tư; bật công khai là thao tác riêng của owner. Một món có thể thuộc nhiều bộ, cặp thành viên có khóa duy nhất. Favorite và membership độc lập: bỏ lưu không xóa món khỏi bộ, xóa một bộ không ảnh hưởng bộ khác.

Chooser mở từ thẻ món, feed hoặc chi tiết; có form/link dự phòng không JavaScript, tạo bộ và thêm món trong cùng luồng. Profile chỉ hiển thị các bộ công khai. Bộ riêng không xuất hiện trong URL danh sách, payload, cover/count hoặc hồ sơ của người khác. Dữ liệu ẩn không được dùng làm ảnh bìa/count.

## Giai đoạn 3: thành phẩm và an toàn cộng đồng

[Thành phẩm và kiểm duyệt](COOKED_POSTS_MODERATION.md) mô tả CRUD, media, báo cáo và moderator. Bài gắn công thức gốc còn hiển thị, một ảnh, ghi chú tối đa 1000 ký tự; chủ bài sửa/xóa. Tạo bài và thông báo tác giả công thức nằm trong cùng transaction, không tự thông báo, có khóa gửi và event key chống trùng.

Hai feed dùng một truy vấn `UNION ALL` với projection công khai, giới hạn 9 dòng để trả tối đa 8. Tổng thứ tự là `CreatedAt DESC, Kind DESC, Id DESC` (`0` công thức, `1` thành phẩm). JavaScript deduplicate bằng loại + ID, không dùng RecipeId làm danh tính bài thành phẩm. Cursor v2 mang cả loại bài; cursor cũ được hiểu tại vị trí công thức tương ứng, tải lại đầu feed để nhận bài mới trước mốc đó. Following lấy danh tính từ claims và áp dụng quan hệ Follow ở mỗi truy vấn.

Ẩn công thức chặn cả bài thành phẩm phụ thuộc. Ẩn bài chặn feed, hồ sơ, gallery, link trực tiếp và ảnh. Media riêng không có endpoint chỉ theo tên file; mỗi lần đọc kiểm tra bài và công thức. Moderator có route xem riêng theo role và các quyết định được ghi cùng giao dịch với trạng thái hiển thị. Role chỉ cấp qua CLI quản trị, không tự đăng ký từ web.

## Migration, xóa và triển khai

1. `20261109154259_AddSocialV2`: bổ sung cờ ẩn/cột tìm kiếm, bộ sưu tập/thành viên, thành phẩm, báo cáo, nhật ký và FK thông báo; backfill tìm kiếm.
2. `20261109154451_AddSocialSearchIndexes`: extension `pg_trgm` và ba chỉ mục GIN.

Áp dụng lên database Development đã cấu hình:

```bash
dotnet ef database update --project CookShare/CookShare.csproj -- --environment Development
dotnet ef migrations has-pending-model-changes --project CookShare/CookShare.csproj
```

Migration chỉ bổ sung schema, không xóa dữ liệu tương tác cũ. Test nâng cấp tạo database riêng ở migration cũ, lưu snapshot chín bảng, nâng cấp rồi đối chiếu từng bảng và kiểm tra backfill/index. `Down()` làm mất dữ liệu tính năng mới; rollback ứng dụng không tự hạ schema. Sao lưu database và hai kho ảnh trước khi triển khai.

Xóa công thức xóa membership và thành phẩm phụ thuộc; xóa tài khoản dọn cả dữ liệu ẩn, bộ của owner và bài của người khác trên công thức bị xóa. Bộ/recipe của người khác được giữ. Báo cáo/nhật ký giữ định danh nội dung để tra cứu, danh tính người bị xóa chuyển NULL. Ảnh mới bị dọn khi transaction lỗi; ảnh thay/xóa được dọn sau commit, file mồ côi có lệnh đối chiếu sau 24 giờ. Xem chính sách chi tiết ở tài liệu tính năng.

Không push, mở PR hoặc deploy trong lần triển khai này. Khi maintainer tích hợp, chạy đủ CI và cấp role/storage theo tài liệu; không đưa fixture hay credentials của môi trường thật vào Git.

## Kiểm chứng và giới hạn

Baseline: 531 test PostgreSQL đạt, 1 browser test opt-in bỏ qua. Kết quả mới nhất và ảnh nằm tại [biên bản kiểm chứng](ui/social-v2/README.md). Các bộ test thêm kiểm tra quyền bộ riêng, lặp membership, cursor hỗn hợp, literal tìm kiếm, rollback thông báo/media, URL ảnh ẩn, role/CSRF thực, dữ liệu ẩn khi xóa tài khoản và nâng cấp schema có dữ liệu.

Kiểm tra trình duyệt dùng Chrome trên Windows, ở 360/390/768/1440 px. Safari/iOS chưa được chạy; chưa kiểm chứng thiết bị vật lý, SMTP thật hoặc triển khai nhiều instance. Upload kiểm tra signature/kích thước header, không re-encode/quét malware. Rate limit trong tiến trình cần bổ sung tầng proxy khi triển khai nhiều instance. Chỉ mục và phân trang đã kiểm tra trên fixture; chưa có benchmark tải production hoặc triệu bản ghi.
