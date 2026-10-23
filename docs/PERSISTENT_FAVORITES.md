# Issue #94 — Saved Recipes và Favorites

## Audit và phạm vi

- Đã đọc `CONTRIBUTING.md`, `.editorconfig`, schema/migrations, controllers, các trang recipe/profile và hạ tầng test.
- Workspace ban đầu ở `feature/persistent-interactions`, có thay đổi chưa commit cho UI database và Issue #76. Bản sao các file này và diff được giữ trong `artifacts/issue94/baseline/` và `baseline.patch` (ignored).
- Fetch phát hiện `develop` mới nhất là `8340b38`, gồm PR #106 Notifications. Nhánh `feature/persistent-favorites` được tạo từ commit này, mang theo phần việc cục bộ và giải quyết conflict ở Details. Không commit, push hay merge vào develop.
- Home, Explore, Details và Profile trong phần việc cục bộ đã dùng recipe thật. Favorites tiếp tục dùng các trang/card đó; không ghép ID demo với ID PostgreSQL.
- Entity `Favorite`, `ApplicationDbContext.Favorites`, quan hệ và unique index `IX_Favorites_UserId_RecipeId` đã có. Issue #94 không cần sửa schema, connection string hoặc migration.

## Luồng lưu dữ liệu

| Endpoint | Kết quả |
| --- | --- |
| `POST /Recipe/{recipeId}/Favorites` | Save idempotent cho tài khoản Identity hiện tại |
| `POST /Recipe/{recipeId}/Favorites/Unsave` | Chỉ xóa Favorite của tài khoản hiện tại |
| `GET /Recipe/Favorites/State?recipeIds=1&recipeIds=2` | Trạng thái các ID đang hiển thị và tổng số Favorite của tài khoản hiện tại |
| `GET /Recipe/Saved` | Render bộ sưu tập riêng từ EF Core/PostgreSQL, mới lưu trước |

Tất cả endpoint Favorites và trang Saved yêu cầu đăng nhập. Các POST có anti-forgery token; UserId lấy từ claim Identity, không nhận từ browser. Cookie challenge cho phép quay về trang hiện tại sau đăng nhập. AJAX xử lý cả redirect đăng nhập và HTTP 401.

Save kiểm tra recipe tồn tại, tạo `Favorite` rồi `SaveChangesAsync()` qua Npgsql hiện có. Unique index xử lý cả hai yêu cầu Save đồng thời; chỉ lỗi trùng đúng index Favorites được coi là idempotent. Unsave giới hạn cả UserId và RecipeId, xử lý an toàn khi yêu cầu khác đã xóa quan hệ. Không xóa recipe hoặc Favorite của người khác.

Phản hồi mutation chứa `{ recipeId, isSaved, savedCount, message }`, truy vấn lại trạng thái thực sau khi ghi. Recipe mất trả 404; lỗi database trả 503 cùng thông báo chung, không trả lỗi nội bộ. GET State trả thêm UserId của chính người gọi để phát hiện đổi tài khoản trong tab cũ. Các trang chứa trạng thái cá nhân và API Favorites có `Cache-Control: no-store`.

Saved dùng truy vấn async, `AsNoTracking()`, Include Recipe/User trước Select, sắp xếp Favorite.CreatedAt rồi Id giảm dần. Trạng thái Save trên Home/Explore/Details/Profile lấy theo lô; không truy vấn từng card. Favorites trên Profile chỉ truy vấn và hiển thị cho chủ tài khoản.

## Tái sử dụng giao diện Issue #45

- Giữ bookmark SVG, `.recipe-save-btn`, `.card-save`, `.saved`, `.saved-card-unsave-btn`, `.btn-details-save`, hover/focus, toast và bố cục responsive.
- Giữ header, toolbar, count, grid và empty state gốc của Saved. `_RecipeCard` hiện có render recipe database, tên/mô tả/category/ảnh và Details link thật.
- `_RecipeSaveButton` dùng chung ở card, Details và bộ sưu tập Profile; form có CSRF và trạng thái ban đầu từ server, dùng được khi tải trang trước JavaScript.
- `recipe-saved.js` khóa các nút cùng RecipeId khi request đang chạy. Chỉ đổi trạng thái/count hoặc bỏ card sau phản hồi hợp lệ. Save/Unsave và Like/Unlike chỉ hiển thị một toast phía dưới cho thành công hoặc lỗi, không hiện dòng thông báo cạnh nút. Toast dùng vùng thông báo truy cập được bằng trình đọc màn hình; lỗi giữ trạng thái cũ.
- Khi quay về tab hoặc khôi phục bfcache, đọc lại trạng thái server. Nếu đổi tài khoản/hết phiên, ẩn bộ sưu tập cũ, khóa form cũ và cung cấp link tải lại/đăng nhập. Nếu có recipe mới được lưu ở tab khác, thông báo tải lại để nạp đủ card.
- Không tải `recipe.js`, `recipe-likes.js`, `recipe-ratings.js` legacy trên Saved database. Giữ nguyên mã nguồn Ratings. Likes độc lập với Favorites.

`cookshare_saved_recipes_v1` và dữ liệu recipe cũ được giữ nguyên trong browser. Module Favorites mới không đọc, ghi, xóa hoặc upload dữ liệu đó. Các ID cũ không được bảo đảm trùng với recipe database; không tự gán cho bất kỳ tài khoản nào. Migration dữ liệu legacy cần thiết kế và phê duyệt riêng.

## Bảo toàn các thay đổi tích hợp

Phạm vi tích hợp gồm UI recipe database, Issue #76, Issue #94 và bản sửa thông báo trùng theo yêu cầu đưa toàn bộ thay đổi lên cùng nhánh. Để giữ Notifications mới nhất hoạt động với AJAX Like/Comments, giữ endpoint tương thích upstream, nối các controller tương tác với NotificationService trong transaction quan hệ, và giữ anchor bình luận cho deep link. Các test PostgreSQL upstream được đặt sau cơ chế opt-in hiện có để mặc định không kết nối database cấu hình của ứng dụng.

Các file chính của Issue #94:

- Thêm `CookShare/Controllers/RecipeFavoritesController.cs`, `CookShare/Data/FavoriteQueries.cs`, `CookShare/Views/Shared/_RecipeSaveButton.cshtml`.
- Sửa HomeController, RecipeController, ProfileController; Home/Index, Recipe/Index/Details/Saved, Profile/Index và `_RecipeCard`.
- Sửa `recipe-saved.js`; bổ sung CSS loading/focus/status trong `recipe-saved.css`, giữ các rule thiết kế cũ.
- Thêm `RecipeFavoritesControllerTests`, `RecipeFavoritesIntegrationTests`, `RecipeFavoritesPostgresTests` và bộ kiểm thử Chrome trong `CookShare.Tests/Frontend/`.
- Điều chỉnh test Saved cũ sang async và thêm tài liệu này.

## Kiểm chứng thực tế

- `dotnet restore CookShare.sln`: thành công.
- `dotnet build CookShare.sln`: thành công; còn cảnh báo analyzer sẵn có trong DbContext/migrations và cấu hình documentation.
- `dotnet test CookShare.sln`: **293 passed, 15 skipped, 0 failed**. Riêng Favorites có 29 test đạt, 1 PostgreSQL opt-in bị bỏ qua.
- HTTP tests dùng EF InMemory trong host cô lập, thay đăng ký DbContext trước khi resolve; không mở connection Neon của ứng dụng. Bao phủ hai tài khoản, chống giả mạo UserId/RecipeId, CSRF, trùng lặp, count, riêng tư, sort, XSS, empty state, trạng thái trên các trang và đăng ký/đăng xuất/đăng nhập bằng cookie Identity thực ở browser session mới.
- Chrome headless chạy JavaScript thật với DOM/CSS và backend giả lập: **40/40 desktop, 40/40 mobile (viewport 390px)**. Kiểm tra pending/duplicate handlers, đồng bộ nút, lỗi mạng/HTTP/JSON, count/empty/focus, login, bfcache/đổi tài khoản và không chạm localStorage. Đây là kiểm thử browser cô lập; chưa phải E2E trên Neon.
- Harness Like/Comments sau bản sửa toast: **29/29 đạt**; Like/Unlike chỉ hiện một toast, đồng thời giữ validation, sửa/xóa, XSS và focus của Comments.
- `git diff --check`: thành công. Kiểm tra style/analyzer toàn solution thành công; whitespace của các file C# đã thay đổi thành công. Whitespace toàn solution trên Windows còn diagnostics CRLF/format từ các file cũ (ví dụ AccountController, migrations), không format hàng loạt ngoài phạm vi.

Lệnh kiểm thử UI có trong `CookShare.Tests/Frontend/README.md`. Kết quả chi tiết nằm trong `artifacts/issue94/` và `CookShare.Tests/TestResults/issue94.trx` (ignored).

Chưa có database PostgreSQL kiểm thử riêng được cung cấp; không chạy test ghi dữ liệu trên Neon, migration/update/reset database hay browser E2E với hai tài khoản Neon. Test `RecipeFavoritesPostgresTests` chỉ chạy khi bật rõ ràng `COOKSHARE_RUN_POSTGRES_TESTS=1` và `COOKSHARE_TEST_CONNECTION` trỏ tới database dùng một lần tên `cookshare_test_*`, đã được chuẩn bị schema riêng. Test này kiểm tra Save/Unsave đồng thời, unique index thực và hai web host khởi động độc lập; tự dọn duy nhất các row thuộc user random của test, không tự migrate/reset schema. Dùng filter `FullyQualifiedName~RecipeFavoritesPostgresTests` để chạy riêng; không bật mặc định trên database dùng chung.

## Git và phạm vi review

Nhánh tích hợp: `feature/persistent-favorites`, dựa trên `develop`. Phạm vi đưa lên gồm toàn bộ UI recipe database, Likes/Comments, Favorites và bản sửa toast; các bản thử tách commit trong `artifacts/` được bỏ qua.

Commit đề xuất: `feat: integrate database recipes, interactions and favorites`.

PR title: `feat: integrate database recipes, likes, comments and favorites`.

Base: `develop`; compare: `feature/persistent-favorites`.

Mô tả đề xuất:

> Home/Explore/Details/Profile dùng recipe database và card Razor dùng chung. Nối Like/Comments và Saved/Profile Favorites với Identity cùng EF Core/Npgsql, giữ UI hiện có và Notifications. Xử lý CSRF, phân quyền, trùng/concurrency, lỗi request và thông báo toast một lần. Restore/build thành công; 293 test đạt, 15 PostgreSQL opt-in bỏ qua; Chrome 40/40 desktop, 40/40 mobile và 29/29 Like/Comments. Chưa kiểm chứng PostgreSQL write/restart trên database riêng; không đổi schema/connection, không chạy database update. Closes #94. Refs #76, #75.
