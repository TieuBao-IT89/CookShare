# UI design workflow — Taste Skill & Impeccable

CookShare dùng hai design skill như công cụ phát triển tùy chọn. Website vẫn là ASP.NET Core MVC/Razor với CSS và JavaScript hiện có; không cần Node hoặc các skill này để build, chạy hay publish ứng dụng.

- [Taste Skill](https://github.com/Leonxlnx/taste-skill) hỗ trợ phân cấp thị giác, bố cục, typography và kiểm tra bằng trình duyệt.
- [Impeccable](https://github.com/pbakaus/impeccable) cung cấp Audit, Critique, Polish và detector. Lệnh chat và lệnh CLI là hai giao diện khác nhau: `/impeccable audit` là yêu cầu cho agent; `impeccable detect` là chương trình chạy trong terminal.

## 1. Môi trường và cài đặt cho Cursor

Cần Git, Node.js **22+**, npm và Chrome/Edge/Chromium. Windows có thể dùng `npm.cmd` nếu execution policy chặn `npm.ps1`; không cần thay đổi execution policy toàn máy. Để chạy ứng dụng/kiểm thử backend cần thêm .NET 8 và database theo README.

Từ thư mục gốc repository:

```powershell
node --version
npm.cmd --version
powershell -NoProfile -ExecutionPolicy Bypass -File tools/design-skills/Install-DesignSkills.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/design-skills/Install-DesignSkills.ps1 -VerifyOnly
```

Script cài trong `.cursor/skills/design-taste-frontend` và `.cursor/skills/impeccable`, kèm bốn agent của Impeccable trong `.cursor/agents`. `.cursor/rules/cookshare-design.mdc` cung cấp ràng buộc CookShare cho file giao diện.

Phiên bản được duyệt nằm trong [lock.json](../tools/design-skills/lock.json): Taste dùng commit Git đầy đủ, Impeccable dùng CLI/skill 4.7.0 và engine 0.1.16. Script tải ZIP release cố định, đối chiếu SHA-256 rồi truyền qua `IMPECCABLE_BUNDLE_PATH`; chỉ ghim phiên bản npm sẽ chưa ghim được bundle skill. Cài xong, script đối chiếu 102 file văn bản đã duyệt, chuẩn hóa khác biệt LF/CRLF khi tính hash.

Script không dùng `--force`, không sửa settings/rules/hooks đã có và không cài global. Khi thư mục skill đã tồn tại, script chỉ xác minh; nội dung khác bản khóa sẽ báo lỗi để người phát triển review, không ghi đè. Installer Impeccable luôn nhận `--providers=cursor --scope=project --no-hooks`. Binary cần để chạy được tải vào vị trí local của skill; không đưa binary, ZIP hoặc cache vào Git.

Khởi động chat Cursor mới/reload cửa sổ sau khi cài để editor nhận skill. Trong Cursor tương thích Agent Skills, kiểm tra skill picker hoặc gọi rõ tên `design-taste-frontend` và `/impeccable audit CookShare/Views/Recipe/Index.cshtml`. Yêu cầu agent đọc đúng skill và tóm tắt target/ràng buộc trước khi chỉnh sửa. Kiểm tra CLI riêng:

```powershell
node .cursor/skills/design-taste-frontend/scripts/draw.mjs --app --list kinds
.\.cursor\skills\impeccable\scripts\impeccable.cmd detect --list-rules
```

Hai lệnh trên kiểm tra script/engine, không chứng minh editor đã kích hoạt skill. Setup issue #120 đã kiểm tra cấu trúc Cursor, metadata, CLI Windows và nội dung file; môi trường thực hiện không có Cursor UI để kiểm tra trực tiếp skill picker. Contributor cần thực hiện bước nhận diện trong editor một lần. Với Codex hoặc agent khác, dùng cơ chế cài project của agent đó; không sửa đường dẫn `.cursor` thành cài global một cách tự động.

## 2. Quy trình Audit và Critique

1. Bắt đầu từ `develop` mới nhất, tạo nhánh riêng. Ghi commit baseline và phạm vi; chọn dữ liệu thật hoặc fixture chỉ nằm trong database kiểm thử riêng.
2. Chạy ứng dụng, kiểm tra Home Feed, Recipe Discovery, Recipe Details, Profile, Create/Edit Recipe và Notifications. Bao gồm khách/chủ tài khoản, có/không có dữ liệu, lỗi validation/ảnh, tải chậm, bàn phím và reduced motion.
3. Chụp Before tại **360px, 768px và 1440px**. Ghi route, viewport, trạng thái đăng nhập và dữ liệu để tái hiện; dùng cùng dữ liệu cho After. Không chụp Secrets hay thông tin kết nối.
4. Dùng Impeccable Audit để ghi các vấn đề có thể đo: contrast, label, focus, touch target, overflow, hiệu năng và token. Critique đánh giá hierarchy, khả năng hiểu/thao tác và mức phù hợp với sản phẩm. Làm theo playbook của phiên bản cài đặt, giữ các đánh giá độc lập khi playbook yêu cầu.
5. Tổng hợp bảng: mức độ P0–P3, trang/selector, cách tái hiện, ảnh/số đo, ảnh hưởng, đề xuất sửa. Phân biệt lỗi thực tế, false positive và lựa chọn thiết kế có chủ đích. Không thay UI trong lúc đang lập audit.

Nguồn vào của tools chỉ là source UI, ngữ cảnh sản phẩm đã công khai và URL localhost. Không cấp User Secrets, `.env`, access token, chuỗi kết nối hay dữ liệu Production cho skill. Không chạy init để tự tạo bối cảnh/claim không được xác nhận; scoped refinement có thể dựa vào code và brief hiện có.

## 3. Implementation: giữ thiết kế hiện có

Dùng Taste Skill để đề xuất cách sửa các vấn đề đã xác nhận. Đây là refinement của CookShare: brief và ràng buộc sản phẩm ưu tiên hơn các khuyến nghị phong cách chung của tool. Không đổi palette/font, thêm video, bỏ toàn bộ card hoặc viết lại Home/Profile/Details chỉ để detector bớt cảnh báo.

Nguồn token chính là `CookShare/wwwroot/css/style.css`:

| Vai trò | Token hiện có |
| --- | --- |
| Forest green | `--primary: #245c43`, `--primary-dark: #173f2e` |
| Chữ/nội dung phụ | `--ink: #243b30`, `--muted: #606b62` (contrast refinement #120) |
| Nền và surface | `--paper: #faf9f5`, `--white: #fff`, `--light: #edf3eb` |
| Accent và lỗi | `--accent: #be6d45` cho trang trí, `--accent-ink: #985130` cho chữ, `--danger: #b33939` |
| Chữ chức năng nhỏ | `--text-small: 12px` cho metadata và thao tác đã audit |
| Typography | `--font`: Be Vietnam Pro; `--font-display`: Playfair Display |
| Spacing | `--space-1`…`--space-8` (4, 8, 12, 16, 24, 32, 48, 64px) |
| Hình khối/chuyển động | `--radius-sm`, `--radius`, `--radius-lg`, `--shadow-sm`, `--shadow`, `--transition` |

Đây là các giá trị đang triển khai, không phải yêu cầu đổi màu sang hex tham khảo trong issue. Ưu tiên token hiện có; chỉ thêm token semantic khi số đo cho thấy cần một vai trò dùng chung mới, ví dụ chữ trên nền accent phải đủ contrast. Giữ Font Awesome và ảnh món ăn hiện có. Không lấy nguyên mẫu từ website bên thứ ba, không thêm frontend framework/dependency chỉ để sửa CSS.

Không sửa database schema, authentication/authorization hay nghiệp vụ CRUD/Like/Comment/Save/Follow/Rating/Notifications trong một PR polish UI. Giữ form action, anti-forgery, ID/data attributes và native submit mà JavaScript/backend đang sử dụng.

## 4. Polish và Responsive Testing

Sau khi sửa, dùng Impeccable Polish để kiểm tra toàn bộ luồng, không chỉ một ảnh. Kiểm tra theo nhóm:

- **360px / 768px / 1440px:** navigation mở/đóng, grid/card, sidebar, form, dialog, nội dung dài; `document.documentElement.scrollWidth <= innerWidth`.
- **Bàn phím:** Tab/Shift+Tab, Enter/Space, Escape cho dialog/menu, focus rõ ràng, thứ tự hợp lý và tên truy cập của nút/input.
- **Đọc nội dung:** contrast chữ thường ≥4.5:1, chữ lớn ≥3:1; label, validation/error và hint liên kết với đúng trường.
- **Cảm ứng:** ưu tiên target 44×44px cho thao tác chính; không coi viewport giả lập là bằng chứng đã test thiết bị thật.
- **Motion:** bật `prefers-reduced-motion`, giữ trạng thái và phản hồi; không thêm animation liên tục hoặc gây layout shift.
- **Chức năng:** gửi form, lỗi/empty/loading, tương tác xã hội, badge và nội dung sau tải lại; ghi rõ các ca chỉ kiểm tra tự động hoặc chưa kiểm tra trên thiết bị thật.

Chạy detector trên các markup target đã sửa (không dùng CSS-only làm target của phiên bản này), xác minh kết quả thay vì cố đạt zero bằng cách làm mất bản sắc thiết kế:

```powershell
.\.cursor\skills\impeccable\scripts\impeccable.cmd detect --json CookShare/Views
node .cursor/skills/design-taste-frontend/scripts/inspect.mjs http://localhost:5051 --app --widths 1440,768,360 --out artifacts/ui-review/home
dotnet build CookShare.sln --configuration Release
dotnet test CookShare.sln --configuration Release --no-build
git diff --check
```

Detector trả exit **2** khi có findings; exit **1** là lỗi không quét được. Ghi findings và các ngoại lệ có bằng chứng. Chạy các browser harness liên quan theo [Frontend README](../CookShare.Tests/Frontend/README.md), PostgreSQL tests trên database kiểm thử riêng và CI của repository.

## 5. Before/After và Code Review

Lưu browser profiles, báo cáo detector thô và ảnh đang chọn trong `artifacts/` hoặc `.taste-skill/` (ignored). Chỉ đưa ảnh review có chọn lọc cùng báo cáo đã loại thông tin nhạy cảm vào `docs/ui/issue120/`. Không copy hàng loạt cache vào docs. Trước/sau phải cùng route, viewport, vị trí cuộn và trạng thái dữ liệu; nếu khác phải chú thích.

Tách PR cấu hình tools/tài liệu khỏi PR audit và sửa UI. PR UI phải liên kết từng thay đổi với finding, liệt kê khu vực đã audit nhưng giữ nguyên, cách test, hạn chế và ảnh Before/After. Không đánh dấu issue hoàn thành chỉ vì PR cài tools đã merge.

## 6. Hooks, cập nhật và giấy phép

Hooks mặc định **tắt**. Bật hook là một thay đổi cấu hình riêng cần được review và chấp thuận; quy trình này không bật hook. Trước khi cập nhật, đọc release/source, kiểm tra file sẽ tạo và giữ cấu hình Cursor hiện có. Cập nhật `lock.json` cùng notices trong PR riêng, thử cài sạch và chạy verify. Không chạy upgrade tự động trên môi trường của contributor.

[THIRD_PARTY.md](../tools/design-skills/THIRD_PARTY.md) ghi nguồn và giấy phép MIT/Apache 2.0 cùng notices. Installed code được bỏ qua khỏi Git; các tài nguyên tool có thể tìm thấy vẫn có giấy phép riêng. Khi đưa code/ảnh/font bên thứ ba vào ứng dụng, kiểm tra và giữ attribution của chính tài nguyên đó.
