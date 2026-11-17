# CookShare Premium V2

> Báo cáo lịch sử của lần triển khai Premium V2 ban đầu. Bản tiếp theo đổi bộ chọn
> theme thành công tắc và làm mới cả hai bảng màu; xem [Theme refresh](THEME_REFRESH.md)
> và [ảnh trước/sau](ui/theme-refresh/README.md). Yêu cầu mới của maintainer cho phép
> push và merge phiên bản hiện tại lên `main`; các ghi chú không push/merge bên dưới
> mô tả phạm vi của lần triển khai trước.

Baseline: `243039c`, kế thừa Social 2.0 và `develop` (`2d125b4`). Nhánh triển khai:
`feature/premium-social-v2`. Workspace sạch trước khi bắt đầu. Không push mã nguồn,
merge, seed hoặc cập nhật database ứng dụng trong lần triển khai này.

[Danh sách đầy đủ file sửa/tạo/xóa](PREMIUM_V2_FILES.md) · [Ảnh review](ui/premium-v2/README.md)

## Audit trước triển khai — 17/11/2026

| Khu vực | File chịu trách nhiệm và kết luận |
| --- | --- |
| Runtime, DI, xác thực | `Program.cs`: .NET 8 MVC, PostgreSQL/Npgsql, Identity, cookie, SMTP, storage và rate limiting đã có. Giữ nguyên chính sách và middleware. |
| Database | `Data/ApplicationDbContext.cs`, configurations và migrations: có đủ Recipes, RecipeLikes, RecipeComments, Favorites, Follows, RecipeRatings, Notifications, collections và cooked posts. Có query filter cho nội dung ẩn. Không cần schema mới cho theme. |
| Feed | `HomeController`, `RecipeFeedQueries`, `HomeIndexViewModel`, `_FeedPost`, `home-feed.js`: feed Latest/Following thật, cursor và chống gửi lặp đã có. Thiếu attribution nguồn ngoài trong projection và feed. |
| Discovery | `RecipeController.Index`, `SocialDiscoveryQueries`, `Recipe/Index`: search tiếng Việt, ingredient/author/category/difficulty/time, sort và cursor thật. Projection bỏ metadata nguồn nhập. Giữ truy vấn, bổ sung metadata hiển thị. |
| Chi tiết và editor | `RecipeController`, `Recipe/Details`, `_RecipeForm`, `issue108.js`, serving scaler/cooking mode: CRUD, validation, preview, checklist và nấu từng bước đã có. Avatar mặc định đang dùng ảnh người mẫu, cần fallback trung tính. |
| Profile | `ProfileController`, `ProfileContent`, `creator-profile.js`: số liệu Identity/Follow/Recipe thật, owner controls và cover có sẵn. Giữ quyền và dữ liệu. |
| Tương tác | Controllers Like/Comments/Favorites/Ratings, `NotificationService`, `recipe-interactions.js`, `recipe-saved.js`: server là nguồn sự thật, CSRF và pending state có sẵn. Cải thiện feedback, không tạo API trùng lặp. |
| Thông báo | `NotificationsController`, `NotificationBadgeViewComponent`: count, read/open theo recipient thật. Không có realtime giả. |
| Import | `Data/Seeding`: CLI có giới hạn Development/Testing, nguồn local/TheMealDB, không seed lúc startup. Không chạy seed trên database ứng dụng. |
| Layout/theme | `_Layout`, `_RecipeHeader`, `style.css`, CSS theo trang: chỉ light mode, nhiều màu hardcode, header blur, chữ phụ rất nhỏ; thiếu theme persistence và control. Cần semantic tokens toàn site. |
| Icon | FA local và SVG sprite tự vẽ đang trộn; JS dựa vào class active FA. Chuyển sang subset Lucide SVG, giữ class trạng thái để tương thích interaction. |

Không tìm thấy dữ liệu tương tác ngẫu nhiên hoặc recipe hardcode trong Views/JS.
Các sample recipes chỉ nằm ở seed/test fixture, không được dùng làm fallback UI.
Category taxonomy và placeholder nhập liệu là nhãn chức năng, không phải dữ liệu giả.

## Nghiên cứu và lựa chọn

Đã xem các repository gốc theo brief:

- [shadcn/ui](https://github.com/shadcn-ui/ui): semantic tokens, focus và phân cấp control; chuyển pattern sang Razor.
- [Magic UI](https://github.com/magicuidesign/magicui), [awesome-shadcnui](https://github.com/2-fly-4-ai/awesome-shadcnui): xem pattern chuyển trạng thái; bỏ hiệu ứng trang trí liên tục và React runtime.
- [Lucide](https://github.com/lucide-icons/lucide), [Tabler](https://github.com/tabler/tabler-icons), [Font Awesome](https://fontawesome.com/): chọn subset SVG Lucide local, không tải runtime/bộ icon đầy đủ. Giữ class FA như compatibility hook, không dùng font để vẽ icon.
- [GSAP](https://github.com/greensock/GSAP), [anime.js](https://github.com/juliangarnier/anime): không thêm dependency vì CSS/Web Animations đủ cho thao tác hiện tại.
- [mise](https://github.com/dacrab/mise): tham khảo thứ tự thông tin bài nấu và bookmark, giữ kiến trúc MVC/Identity hiện có.

## Nguồn dữ liệu giữ nguyên

Recipe → Recipes; like → RecipeLikes; comment → RecipeComments; save → Favorites;
follow → Follows; rating → RecipeRatings; notification → Notifications; profile →
Identity Users. Featured/popular/top rated dùng công thức xếp hạng sẵn có từ tương
tác thật; empty state được giữ khi không có dữ liệu. Collections/cooked posts và
moderation kế thừa nguyên trạng từ baseline.

## Kết quả triển khai

- `tokens.css` định nghĩa Warm Culinary/Midnight Kitchen, màu semantic, foreground trên primary/danger, spacing, typography, radius, shadow, focus và motion. Các stylesheet theo trang dùng token thay màu cố định. `premium.css` điều chỉnh bố cục và hierarchy theo từng màn hình.
- Header gọn, tìm kiếm thật, avatar tài khoản thật qua projection chỉ lấy AvatarUrl; bộ chọn giao diện native hỗ trợ bàn phím và screen reader. Mobile có theme, thông báo và menu ở đầu, điều hướng chính ở dưới.
- Feed ba vùng trên desktop; hai vùng ở 960–1199px; một vùng trên mobile. Composer dùng avatar thật, metadata của bài viết bớt badge, vùng đọc lớn hơn. Sidebar đề xuất lấy dữ liệu hiện hữu và có empty state.
- Discovery giữ search/filter/sort/cursor thật, cải thiện typography, category chips, khung ảnh và khoảng cách. Recipe Details có ngày đăng, cooking action nổi bật, ảnh không lấn cột, share/copy fallback dialog; giữ checklist, scaler, cooking mode và thảo luận.
- Editor hai cột/đơn cột và auth/profile/notifications/collections có cùng token theme; giữ upload preview, validation, form POST/CSRF và trạng thái pending sẵn có. Follow native POST thêm chống nhấn lặp.
- Icon local chuyển sang 56 Lucide SVG symbol, tổng sprite 12.009 byte. TagHelper chuyển markup legacy thành SVG ngay trên server; class `fa-*` giữ trạng thái tương thích với JS. Không còn tải Font Awesome trong layout, không thêm React, GSAP, anime hoặc runtime icon.
- Thay emoji ở bình luận và nút sửa/xóa bằng SVG, kể cả bình luận được thêm qua AJAX. Giữ Be Vietnam Pro/Playfair Display local và giấy phép hiện có.
- Avatar thiếu dữ liệu dùng hình người trung tính thay ảnh một người có thật; ảnh món thiếu dữ liệu dùng illustration nền trong suốt phù hợp cả hai theme.

### Theme hoạt động thế nào

`theme-init.js` chạy đồng bộ trong head, trước CSS. Chỉ preference `light` / `dark` /
`system` được lưu ở `localStorage['cookshare.theme']`. Theme áp dụng qua
`html[data-theme]`; system theo `prefers-color-scheme`, có đồng bộ giữa các tab.
Storage bị chặn vẫn dùng preference hệ thống. Không JavaScript thì CSS dùng theme
hệ thống và ẩn control không hoạt động. Chuyển màu ngắn, tôn trọng reduced motion;
native select có tên truy cập và live status. Không dùng storage làm nguồn dữ liệu
công thức, tài khoản hoặc tương tác.

### Dữ liệu thật và attribution

Audit không tìm thấy số liệu ngẫu nhiên trong UI hiện tại nên không xóa/reset dữ
liệu database để “làm sạch”. Sửa thiếu metadata `Source/ExternalId/SourceUrl` trong
projection Discovery và feed, hiển thị TheMealDB và phân biệt người nhập với tác giả
công thức. Thành phẩm người dùng tự chụp vẫn là nội dung của người đăng, không bị
gắn nguồn nhập của công thức gốc. Thêm test kiểm tra nguồn, người nhập và zero
engagement thật. Không thêm seed lúc startup hoặc dữ liệu demo vào Views/JS.

Các controller nghiệp vụ Like/Comment/Save/Follow/Rating/Notification, Identity,
phân quyền chủ sở hữu, chống CSRF, media validation, rate limiting, seeding guard,
collection privacy và moderation được giữ nguyên. Không có migration mới trong
Premium V2; hai migration Social 2.0 kế thừa từ baseline vẫn cần có ở database chạy
ứng dụng. Không áp dụng migration lên Neon/Development trong lần làm này.

## Kiểm chứng ngày 17/11/2026

| Kiểm tra | Kết quả |
| --- | --- |
| `dotnet build CookShare.sln --configuration Release` | Thành công, 0 lỗi; 21 warning đã tồn tại ở baseline, không thêm warning mới. |
| `dotnet test CookShare.sln --configuration Release --no-build` với PostgreSQL và browser opt-in | **614 passed, 0 failed, 0 skipped**. PostgreSQL riêng `cookshare_test_social_v2` trên loopback. |
| `premium-ui.py` | **154 tổ hợp**: 11 trang × 7 viewport × 2 theme; HTTP 200, không tràn ngang, dùng SVG. **14 behavioral checks**: theme reload/cross-tab/system, storage bị chặn, no-JS fallback, keyboard, Escape, reduced motion và không có uncaught JS error. |
| Viewport | 360 / 390 / 768 / 960 / 1024 / 1366 / 1920px. Thêm 960/1024 để kiểm tra breakpoint giữa tablet và laptop. |
| `premium-interactions.py` | **22 ca**: server validation giữ nội dung, create/edit, checklist/scaler/cooking, share fallback/focus, like/count, comment, SVG, save/unsave, rating, follow/unfollow, notification/read, guest Identity, CSRF và cleanup. Trạng thái tương tác được đọc lại sau reload. |
| Harness feed | Desktop **30**, Mobile **30** passed. |
| Harness UI refinement / discovery / creator | Mobile lần lượt **20 / 16 / 32** passed. |
| Harness Favorites / Rating | Mobile lần lượt **40 / 20** passed. |
| JS syntax / whitespace | `node --check` các module sửa, `git diff --check`, .NET whitespace trên file C# mới: đạt. |
| Contrast semantic | Cặp ink/muted/primary/accent-ink trên surface, muted trên light và on-primary trên primary đều ≥ **4,63:1** ở light, ≥ **5,90:1** ở dark trong các cặp đã đo. Không coi đây là chứng nhận WCAG toàn site. |

Log thô nằm ở `artifacts/social-v2/premium-*.log`; dữ liệu JSON và ảnh QA đầy đủ ở
`artifacts/premium-v2/` (ignored). [Ảnh review chọn lọc](ui/premium-v2/README.md)
ghi rõ dữ liệu fixture; không phải ảnh production. Script interaction tạo công
thức tạm trong fixture và dọn qua endpoint owner, không can thiệp database ứng dụng.

Chưa kiểm tra trên Safari/iOS/Android vật lý hoặc database Neon thật. Không có
deployment/push source trong lần này. Chức năng production chỉ được xác nhận tới
mức build, test PostgreSQL riêng và Chrome headless nêu trên.

## Chạy và kiểm tra

1. Dùng .NET 8 và PostgreSQL theo [SETUP](SETUP.md); cấu hình connection qua User Secrets/environment. Database cần schema Social 2.0 mà nhánh này kế thừa.
2. Chạy `dotnet run --project CookShare/CookShare.csproj`. Truy cập URL Kestrel thông báo.
3. Kiểm tra `/`, `/Recipe`, `/Recipe/Details/{id}`, `/Recipe/Create`, `/Profile`, `/Notifications` và các trang Account. Chọn Sáng/Tối/Thiết bị ở header, chuyển trang và reload.
4. Với dữ liệu trống, giữ empty state. Để chạy lại hai script QA, dùng **fixture Social V2 riêng trên loopback** có tài khoản kiểm thử, không trỏ script tới môi trường ứng dụng. Script không tự tạo database hay seed tài khoản. Playwright Python và Chrome chỉ là công cụ QA, không là dependency runtime.
5. Có thể chạy các harness cô lập theo [Frontend README](../CookShare.Tests/Frontend/README.md) mà không cần database.

## Nhánh và khôi phục

Đã xóa 6 nhánh local cũ và 12 nhánh remote cũ theo yêu cầu trực tiếp. Remote chỉ
còn `main`, `develop`; local thêm `feature/premium-social-v2` để triển khai.
Không thay đổi commit của main/develop, không push source, merge hay force-push.
Các commit Social 2.0 chưa merge được giữ trên nhánh mới. Bundle đầy đủ trước khi
xóa: `E:\Project-OSS\CookShare-branches-before-premium-v2-20261117.bundle` (đã verify).

Khôi phục một nhánh khi cần bằng:

```powershell
git fetch E:/Project-OSS/CookShare-branches-before-premium-v2-20261117.bundle refs/heads/feature/cookshare-social-v2:refs/heads/feature/cookshare-social-v2
```
