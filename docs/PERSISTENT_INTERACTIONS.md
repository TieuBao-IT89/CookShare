# Issue #76 — Likes và Comments dùng database

> Phạm vi tích hợp hiện tại gồm cả UI recipe database, Likes/Comments, Favorites và bản sửa toast trên nhánh `feature/persistent-favorites`, theo yêu cầu đưa toàn bộ thay đổi lên cùng nhánh. Các mục về snapshot và số liệu 253 test bên dưới ghi nhận giai đoạn Issue #76 trước đó; xem [PERSISTENT_FAVORITES.md](PERSISTENT_FAVORITES.md) cho phạm vi review và kết quả tổng hợp mới nhất. Like/Unlike hiện chỉ hiển thị một toast phía dưới, không có thông báo cạnh nút.

## Phạm vi và phụ thuộc Git

Nhánh triển khai: `feature/persistent-interactions`, tại cùng commit với `develop`/`origin/develop` khi bắt đầu.

Workspace trước đó có 32 tệp chưa commit từ `feature/recipe-db-ui-integration`. Phần tích hợp này đã được triển khai/kiểm thử nhưng chưa commit hoặc merge; Issue #76 dùng các thẻ công thức và trang Home database của phần đó. Không stash, bỏ hoặc commit các thay đổi trước. Snapshot và patch nền được lưu trong `artifacts/issue76/baseline/`, `baseline.patch` và `baseline-manifest.json` (Git bỏ qua).

Diff hiện tại gồm cả hai phần. Cần hoàn tất/tách phần nền trước khi mở PR #76 độc lập vào `develop`. Không có commit, push hoặc PR tự động.

## Dữ liệu và bảo mật

- Tái sử dụng `RecipeLike`, `RecipeComment`, các DbSet, unique index `(UserId, RecipeId)` và khóa ngoại hiện có. Không đổi entity, DbContext, schema, migration, `Program.cs`, connection settings hoặc User Secrets.
- Likes/Comments lấy UserId từ Identity; không nhận tác giả từ trình duyệt. Các ID được bind bằng `[FromRoute]` để form không ghi đè ID trên URL.
- Mọi thao tác ghi yêu cầu đăng nhập và anti-forgery token. Edit/Delete Comments yêu cầu đúng tác giả và đồng thời đúng RecipeId/CommentId.
- Nội dung bình luận được trim và giới hạn 500 ký tự, trong giới hạn 1000 ký tự của cột hiện có. Edit giữ nguyên ID, tác giả và CreatedAt. Không thêm UpdatedAt hoặc nhãn sửa giả.
- Các trang/API dùng tổng Like thật; không thêm baseline demo. Unique constraint xử lý Like đồng thời; yêu cầu Like/Unlike lặp lại trả trạng thái thực tế. Lỗi database trả thông báo an toàn; không đưa exception/credentials vào response.
- Razor encode nội dung; JavaScript dùng `textContent`, kiểm tra URL avatar và phản hồi JSON. API chỉ trả thông tin tác giả công khai, không trả email/phone/password hash.

## Endpoint

| Method | Đường dẫn | Chức năng |
| --- | --- | --- |
| GET | `/Recipe/{recipeId}/Likes` | Tổng Like và trạng thái của người xem |
| POST | `/Recipe/{recipeId}/Likes` | Like idempotent |
| POST | `/Recipe/{recipeId}/Likes/Unlike` | Xóa Like của chính người đăng nhập |
| GET | `/Recipe/{recipeId}/Comments` | Danh sách bình luận công khai |
| POST | `/Recipe/{recipeId}/Comments` | Tạo bình luận; trường `Content` |
| POST | `/Recipe/{recipeId}/Comments/{commentId}/Edit` | Sửa bình luận sở hữu |
| POST | `/Recipe/{recipeId}/Comments/{commentId}/Delete` | Xóa bình luận sở hữu |

Khách chưa đăng nhập được đọc dữ liệu; thao tác ghi dùng cookie challenge tới Login. UI hiển thị liên kết đăng nhập kèm returnUrl. JavaScript nhận biết redirect/401 khi phiên hết hạn, giữ dữ liệu nhập và không báo thành công giả. Nội dung không hợp lệ trả 400; sai quyền trả 403; thiếu bản ghi trả 404; lỗi database trả 503.

## Tích hợp UI

- `_RecipeLikeButton` dùng lại lớp `.recipe-like-btn`, `.recipe-like-count-display`, `.liked` và CSS Like hiện có. Có trái tim, tổng thực, aria-pressed, trạng thái chờ và lỗi.
- Home, Explore, Details và thẻ Profile dùng cùng component. Truy vấn projection lấy counts/trạng thái theo lô một lần cho tập ID; không truy vấn riêng từng thẻ. Like trên một thẻ cập nhật các thẻ cùng ID đang hiển thị.
- `_RecipeComments`/`_RecipeCommentItem` dùng lại CSS Comments, composer 500 ký tự, bộ đếm/cảnh báo, avatar/initials, tác giả thật, timestamp, sửa trực tiếp và xác nhận xóa. Danh sách ban đầu do Razor render; không cần bộ dựng Details demo.
- `recipe-interactions.js` xử lý gửi request, CSRF, chống gửi lặp, cập nhật sau thành công và phục hồi focus bàn phím. Lỗi giữ nguyên trạng thái/nội dung.
- Các script legacy `recipe-likes.js`, `recipe-comments.js` và các key localStorage cũ được giữ nguyên. Favorites sau đó đã chuyển sang database trong Issue #94; Ratings vẫn giữ module hiện có. Dữ liệu browser cũ không tự nhập vào tài khoản.

## Kiểm chứng đã thực thi

- Restore và build: đạt. Lần biên dịch đầy đủ còn các cảnh báo sẵn có tại DbContext/migration và cấu hình documentation.
- xUnit: **253 đạt, 0 thất bại, 10 bỏ qua**, tổng 263. Trong đó Issue #76 thêm **23 test Comments, 7 test Likes và 40 test HTTP integration**.
- HTTP tests dùng EF InMemory biệt lập; bao gồm đăng ký/đăng nhập/đăng xuất bằng cookie thật, CSRF, quyền sở hữu, giả mạo ID, trạng thái sau tải lại, validation, markup an toàn và hồi quy Recipe/Profile.
- Unit tests mô phỏng hai request Like/Unlike tranh chấp, kiểm tra unique index và SQL Npgsql bằng `ToQueryString` không mở kết nối. Chưa chạy race test trên PostgreSQL thật.
- Chrome headless chạy mã JavaScript thực với DOM và phản hồi fetch kiểm soát: **26 đạt, 0 thất bại**. Kiểm tra đồng bộ/chống gửi lặp, lỗi/401/CSRF, validation, XSS, tác giả/ID server, sửa/xóa, focus và không truy cập localStorage. Đây là harness UI, không phải phiên đăng nhập browser kết nối Neon.
- Chạy ứng dụng với cấu hình phát triển hiện có: GET Home, Explore, Details của một công thức thật và hai endpoint GET Likes/Comments đều trả **200**. Tất cả request kiểm tra trực tiếp đều là GET; không ghi dữ liệu thử vào Neon.
- Style, analyzers, whitespace của 14 tệp C# thuộc Issue #76 và `git diff --check`: đạt. Whitespace toàn solution vẫn báo 23 tệp ngoài phạm vi có định dạng sẵn có; không sửa hàng loạt các tệp này.

Kết quả: `CookShare.Tests/TestResults/issue76.trx`, `artifacts/issue76/tests.log`, `js-tests.html` và `js-tests-dom-outside.html`. Các artifact này không được đưa vào Git.

## Giới hạn và kiểm tra còn lại

- Chưa chạy kịch bản ghi dữ liệu A/B trên Neon hoặc kiểm chứng persistence qua khởi động lại bằng PostgreSQL riêng. Không có database thử nghiệm PostgreSQL được xác nhận để chạy write tests.
- 10 test PostgreSQL CRUD hiện có vẫn yêu cầu opt-in bằng `COOKSHARE_RUN_POSTGRES_TESTS=1` và `COOKSHARE_TEST_CONNECTION` trỏ database thử nghiệm riêng. Factory đó có migration/write; không dùng connection Neon chia sẻ.
- Chưa xác minh thủ công toàn bộ trang ở desktop/mobile hoặc GitHub Actions. Các CSS responsive cũ được tái sử dụng; test browser ở đây kiểm chứng hành vi module.
- Không tự chuyển dữ liệu/tác giả của tương tác demo. Migration dữ liệu localStorage cần chiến lược riêng.

Khi có database an toàn và hai tài khoản thử nghiệm: User A Like, refresh, Unlike, tạo/sửa comment; User B đọc và thử sửa/xóa comment A (phải bị chặn), thêm comment B; A xóa comment của mình và xác nhận B còn nguyên. Khởi động lại ứng dụng và kiểm tra dữ liệu; kiểm tra keyboard/mobile/lỗi mạng.

## Các tệp thuộc Issue #76 so với snapshot nền

| Nhóm | Tệp |
| --- | --- |
| Controller mới | `CookShare/Controllers/RecipeLikesController.cs`, `RecipeCommentsController.cs` |
| Controller tích hợp | `CookShare/Controllers/HomeController.cs`, `RecipeController.cs`, `ProfileController.cs` |
| Query mới | `CookShare/Data/RecipeLikeQueries.cs`, `RecipeCommentQueries.cs` |
| ViewModel mới | `CookShare/ViewModels/RecipeLikeViewModel.cs`, `RecipeCommentViewModel.cs`, `RecipeCommentsViewModel.cs`, `CommentFormViewModel.cs` |
| View tích hợp | `CookShare/Views/Home/Index.cshtml`, `Views/Recipe/Index.cshtml`, `Views/Recipe/Details.cshtml`, `Views/Profile/Index.cshtml`, `Views/Shared/_RecipeCard.cshtml` |
| Partial mới | `CookShare/Views/Shared/_RecipeLikeButton.cshtml`, `_RecipeComments.cshtml`, `_RecipeCommentItem.cshtml` |
| Frontend mới | `CookShare/wwwroot/js/recipe-interactions.js`, `CookShare/wwwroot/css/recipe-interactions.css` |
| Test mới | `CookShare.Tests/Controllers/RecipeLikesControllerTests.cs`, `RecipeCommentsControllerTests.cs`, `CookShare.Tests/Integration/RecipeInteractionsIntegrationTests.cs` |
| Tài liệu | `docs/PERSISTENT_INTERACTIONS.md` mới; `docs/RECIPE_DATABASE_INTEGRATION.md` thêm ghi chú chuyển tiếp tới Issue #76 |

## PR đề xuất

Title: `feat: integrate recipe likes and comments with Neon`

Base: `develop`; compare: `feature/persistent-interactions`.

Description:

> Khôi phục Like/Comments trên công thức database bằng Identity và EF Core, tái sử dụng schema và CSS hiện có. Bổ sung tổng Like/trạng thái theo lô trên Home/Explore/Details/Profile, Comments có validation/ownership/CSRF/XSS, xử lý request lặp và tranh chấp Like. Thêm 70 test; toàn bộ suite 253 pass/10 skip, browser module 26 pass; kiểm tra GET với cấu hình phát triển đạt. Chưa kiểm chứng PostgreSQL write/restart hoặc toàn bộ mobile. Phụ thuộc phần recipe-db-ui-integration chưa commit trong workspace; cần tách/tích hợp phần nền trước PR độc lập. Closes #76

Commit đề xuất: `feat: persist recipe likes and comments`.
