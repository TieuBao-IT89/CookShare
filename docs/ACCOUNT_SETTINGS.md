# Account Settings — Issue #101

## Nguồn triển khai và phạm vi

Tính năng tái sử dụng PR [#105](https://github.com/TieuBao-IT89/CookShare/pull/105) của Nguyên Hải (`haivo200c-lang`), head commit `6dd79a9e86e6d9cc6bfd4db3d7f205121c5b39a3`. Lịch sử commit gốc được giữ trong merge vào nhánh tích hợp; không sao chép lại tính năng từ đầu và không ghi lên nhánh của contributor.

Base được kiểm tra: `origin/develop` tại `224f9af`, có Creator Profile, notifications, tương tác bền vững, ratings, favorites, recipe seeding và health check. Không thay đổi schema, không chạy migration hoặc thao tác xóa trên Neon chung.

## Checklist theo Issue #101

| Yêu cầu | PR #105 đã có | Phần hoàn thiện trên develop mới |
| --- | --- | --- |
| `/Account/Settings`, username/email/trạng thái xác minh | Có, yêu cầu đăng nhập | Giữ giao diện xanh, giữ navigation và biểu tượng mới trên develop; xử lý email/tên dài |
| DisplayName và avatar | Dẫn sang `/Profile/Edit` | Giữ duy nhất luồng chỉnh hồ sơ hiện có, kiểm tra quyền và dữ liệu không bị ghi đè |
| Đổi mật khẩu | `UserManager.ChangePasswordAsync`, mật khẩu cũ và xác nhận | Kiểm tra cookie thật, security stamp, quy tắc Identity và dữ liệu hồ sơ |
| Gửi email SMTP | Abstraction, MailKit, thông báo lỗi | URL gốc tin cậy; TLS bắt buộc cho hai chế độ SMTP; timeout; fake sender có lỗi gửi |
| Xác minh email | Identity token và thông báo tiếng Việt | Kiểm tra sai/hết hạn/khác tài khoản; giữ trạng thái xác minh khi reset mật khẩu |
| Đổi email | Mật khẩu hiện tại, token gửi tới địa chỉ mới | Kiểm tra uniqueness, token gắn đúng email/tài khoản, cookie và bảo toàn hồ sơ |
| Quên/đặt lại mật khẩu | Phản hồi thống nhất, token một giờ | Kiểm tra token tái sử dụng, lỗi gửi, đăng nhập lại và vô hiệu phiên cũ |
| Xóa tài khoản | Reauthentication, transaction, tương tác và follows | Xử lý rõ `ProfileFeaturedRecipe`; kiểm tra đủ graph, rollback và đăng xuất bằng cookie thật |
| Authentication/authorization/anti-forgery | Các POST bảo vệ bằng Identity và CSRF | Kiểm tra anonymous, thiếu CSRF và UserId giả từ form |
| Build/tests/CI | Có test PostgreSQL | Giữ test gốc; SQLite relational cô lập mặc định; PostgreSQL riêng khi CI opt-in |

## Hồ sơ và mật khẩu

Settings chỉ đọc `DisplayName`/`AvatarUrl` và liên kết `/Profile/Edit`. Profile controller lấy người dùng từ Identity session, không tin UserId trong form; ViewModel chỉ cho sửa trường hồ sơ. Tên được đọc từ user ở Profile, tác giả công thức, bình luận, thông báo và danh sách theo dõi.

Đổi mật khẩu kiểm tra mật khẩu hiện tại, validation xác nhận và quy tắc Identity. Identity cập nhật security stamp; cookie của phiên hiện tại được refresh. Đổi email yêu cầu mật khẩu hiện tại và chỉ cập nhật sau khi token gắn với user và địa chỉ mới được xác thực. Hồ sơ, công thức và tương tác không bị ghi đè.

Quên mật khẩu trả cùng thông báo cho email tồn tại, không tồn tại hoặc khi email gửi lỗi. Reset dùng token Identity một giờ, từ chối token sai/hết hạn/khác user/đã dùng. Reset không tự xác minh email và không tự đăng nhập. Cookie cũ bị từ chối ở lần kiểm tra security stamp tiếp theo; production giữ mặc định Identity (khoảng 30 phút). Test đặt interval bằng 0 trong host thử nghiệm để kiểm chứng ngay bằng cookie thật.

## Cấu hình email

Đặt qua biến môi trường hoặc .NET User Secrets; bảng chỉ ghi tên cấu hình, không chứa credential thật.

| Biến môi trường | Ý nghĩa |
| --- | --- |
| `Email__PublicBaseUrl` | URL gốc tin cậy của CookShare; HTTPS ngoài Development/Testing; không có credentials, query hoặc fragment |
| `Email__FromAddress` | Địa chỉ người gửi |
| `Email__FromName` | Tên người gửi, mặc định CookShare |
| `Email__SmtpHost` | Máy chủ SMTP |
| `Email__SmtpPort` | Cổng SMTP, mặc định 587 |
| `Email__SmtpUser` | Tài khoản SMTP nếu nhà cung cấp yêu cầu |
| `Email__SmtpPassword` | Mật khẩu/app password SMTP |
| `Email__UseStartTls` | `true`: yêu cầu STARTTLS; `false`: yêu cầu implicit TLS, thường dùng cổng 465 |
| `Email__TimeoutSeconds` | Thời gian chờ SMTP, mặc định 30 giây, giới hạn 1–120 |

User Secrets dùng cùng key với dấu `:` thay cho `__`, trong project `CookShare/CookShare.csproj`. Thiếu URL gốc/SMTP hoặc gửi lỗi không thể được báo như gửi thành công. Không ghi token/password/email body vào log; trang thông báo không công khai token. Token chỉ xuất hiện trong liên kết gửi qua email và trường ẩn của form reset.

Chưa xác minh gửi thư qua nhà cung cấp SMTP thật. Các bài kiểm thử dùng recording sender, không gửi thư ra ngoài.

## Chính sách xóa dữ liệu

Giữ chính sách PR #105: xóa tài khoản và công thức do tài khoản đăng, bao gồm tương tác của người khác trên các công thức đó. Form xác nhận giải thích tác động này trước khi gửi.

| Dữ liệu | Cách xử lý |
| --- | --- |
| ApplicationUser, DisplayName, AvatarUrl, Bio, CoverImageUrl | Xóa đúng user từ session sau khi kiểm tra mật khẩu và checkbox |
| Identity claims, logins, tokens, user-role links | Xóa theo user; role dùng chung được giữ |
| Recipes | Xóa công thức có `UserId` bằng user bị xóa |
| Likes, comments, favorites, ratings | Xóa nếu thuộc user hoặc tham chiếu công thức của user |
| Follows | Xóa cả hai chiều khi user là follower hoặc following |
| Notifications | Xóa khi user là actor/recipient hoặc tham chiếu công thức bị xóa |
| Thông báo giữa user khác chỉ còn tham chiếu bình luận đã xóa | Giữ lịch sử và đặt `CommentId = null` bằng FK `SetNull` hiện có |
| ProfileFeaturedRecipes | Xóa ghim của user và ghim tham chiếu công thức bị xóa |
| User khác, công thức khác, dữ liệu seeding không thuộc user | Giữ nguyên, kể cả hồ sơ, role và tương tác không liên quan |

Các bước bulk delete và `UserManager.DeleteAsync` dùng cùng transaction. Identity thất bại hoặc exception trước commit sẽ rollback. Test tạo lỗi concurrency thật để kiểm tra các phụ thuộc và tác động `SetNull` được khôi phục; không thêm bypass vào ứng dụng.

## Kiểm thử và giới hạn

`AccountSettingsWebApplicationFactory` dùng SQLite in-memory với foreign keys và transaction thật, khóa email giả trong host Testing và data-protection key tạm thời. Không đọc chuỗi kết nối ứng dụng để mở database. Test gốc Account Settings được giữ và chuyển sang fixture này, không bị đánh dấu skip.

Khi CI đặt `COOKSHARE_RUN_POSTGRES_TESTS=1` và `COOKSHARE_TEST_CONNECTION`, fixture Account Settings chỉ chấp nhận host loopback và database thử nghiệm, tự tạo database có GUID riêng và chỉ xóa database do chính fixture sở hữu. Những bộ PostgreSQL hiện có tiếp tục dùng service PostgreSQL disposable trong workflow. CI dùng tên `cookshare_test_ci` để test Favorites đáp ứng guard `cookshare_test_*`; test này thuộc cùng collection có fixture chuẩn bị migration trước khi chạy. Các host thử nghiệm dùng khóa data-protection tạm, không dùng kho khóa cá nhân hoặc Windows Event Log.

Chạy các kiểm tra tại root:

```powershell
dotnet restore CookShare.sln
dotnet build CookShare.sln
dotnet test CookShare.sln
dotnet format whitespace CookShare.sln --verify-no-changes --no-restore
dotnet format style CookShare.sln --verify-no-changes --no-restore --severity warn
dotnet format analyzers CookShare.sln --verify-no-changes --no-restore --severity warn
```

Không đặt `COOKSHARE_TEST_CONNECTION` trỏ Neon chung. Khi chạy toàn bộ PostgreSQL tests, provision database disposable có tên `cookshare_test_*`, bật `COOKSHARE_RUN_POSTGRES_TESTS=1` và đặt chuỗi kết nối loopback tương ứng. CI đã cấp service này. Khi chưa opt-in, các test riêng PostgreSQL tiếp tục được bỏ qua đúng thiết kế, còn Account Settings chạy trên SQLite.

Kiểm tra trình duyệt độc lập:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-AccountSettingsBrowserTests.ps1
```

Để chạy cùng full suite, đặt thêm `COOKSHARE_RUN_BROWSER_TESTS=1`. Host trình duyệt luôn dùng SQLite riêng, kể cả khi các test khác dùng PostgreSQL. Chrome/Edge chỉ truy cập proxy loopback nối tới TestServer; cookie Identity và form Razor là thật, email do recording sender giữ trong bộ nhớ. Proxy local dùng HTTP trên localhost do hạn chế chứng chỉ của sandbox Windows; không kiểm chứng HTTPS staging hoặc SMTP thật. Profile Chromium được xóa sau chạy, không giữ DOM hay URL token trong báo cáo.

Ảnh `docs/evidence/issue-101` được giữ từ PR #105, là minh chứng gốc của contributor. Kết quả kiểm chứng bổ sung của nhánh completion và giới hạn được ghi trong báo cáo cuối; không coi ảnh gốc là lần chạy mới.

## Kết quả kiểm chứng ngày 24/10/2026

| Kiểm tra | Kết quả |
| --- | --- |
| Restore bằng .NET SDK 8.0.318 | Thành công |
| Build Debug và Release | Thành công, không lỗi; clean build có 7 cảnh báo cũ ở ApplicationDbContext/migrations và cấu hình documentation |
| Full Release suite, PostgreSQL 16.15 mới tạo + Chrome opt-in | **495 passed, 0 failed, 0 skipped** |
| Test gốc Account Settings | 11/11 passed, được tái sử dụng |
| Account security / deletion / profile integration | 19 + 6 + 1 passed |
| SMTP transport failure tests | 2/2 passed trên TCP loopback, không gửi thư thật |
| Live browser | 1/1 passed, 17 assertion qua workflow thật và layout 360/768/1440 px |
| Whitespace / style / analyzer verify | Đạt cả ba lệnh theo CONTRIBUTING |
| GitHub Actions trên remote | Chưa chạy vì chưa push/mở lại PR |
| SMTP nhà cung cấp, Azure deployment | Chưa kiểm chứng; cần cấu hình môi trường và kiểm tra riêng |

PostgreSQL portable được tải từ nguồn EDB được PostgreSQL.org dẫn tới, kiểm tra SHA-256 nhà phát hành, chạy chỉ loopback trong `artifacts/issue101`, dùng cluster/database mới tạo. Không dùng Neon hoặc cài service toàn hệ thống. Tất cả 21 test PostgreSQL trước đó được opt-in trong lượt cuối; không bỏ qua hoặc nới assertion để đạt kết quả.

Bằng chứng cục bộ (ignored, không đưa lên Git): `artifacts/issue101/verification.json`, `artifacts/issue101/test-results/issue101-release-postgres-browser.trx`, `artifacts/issue101/test-release.log`, `artifacts/issue101/browser-efb56762256043928eba29912606a445/result.json`. Trình duyệt không tạo screenshot mới. Khóa data-protection và profile browser là tạm; server PostgreSQL được tắt sau xác minh.

## File thêm hoặc sửa

45 file so với develop, gồm 8 ảnh kế thừa PR #105:

- Controller/DI/config: `CookShare/Controllers/AccountController.cs`, `CookShare/Program.cs`, `CookShare/CookShare.csproj`, `CookShare/appsettings.json`.
- Identity/options/services: `CookShare/Identity/VietnameseIdentityErrorDescriber.cs`, `CookShare/Options/EmailOptions.cs`, `CookShare/Services/AccountDeletionService.cs`, `AccountEmailMessages.cs`, `IAccountEmailSender.cs`, `SmtpAccountEmailSender.cs`.
- ViewModel/views: `CookShare/ViewModels/AccountSettingsViewModel.cs`; `CookShare/Views/Account/{Settings,ForgotPassword,ResetPassword,Notice,Login}.cshtml`; `CookShare/Views/Shared/{_Layout,_RecipeHeader}.cshtml`; `CookShare/wwwroot/css/style.css`.
- Test infrastructure: `CookShare.Tests/CookShare.Tests.csproj`, `Integration/{CookShareWebApplicationFactory,AccountSettingsWebApplicationFactory,RecordingAccountEmailSender,RecipeFavoritesPostgresTests}.cs`.
- Account tests: `CookShare.Tests/Integration/{AccountSettingsIntegrationTests,AccountSecurityIntegrationTests,AccountDeletionIntegrationTests,AccountProfileIntegrationTests,AccountSettingsBrowserTests,AccountSettingsBrowserPage}.cs`; `CookShare.Tests/Services/SmtpAccountEmailSenderTests.cs`; `CookShare.Tests/Frontend/Run-AccountSettingsBrowserTests.ps1`.
- CI/tài liệu: `.github/workflows/dotnet-ci.yml`, `CHANGELOG.md`, `README.md`, `SECURITY.md`, `docs/ACCOUNT_SETTINGS.md` và 8 ảnh gốc trong `docs/evidence/issue-101`.

Không sửa ProfileController, ApplicationDbContext, entity, migration hoặc seeder của develop. Việc đổi CRLF sang LF tại working copy theo `.editorconfig` không tạo diff nội dung không liên quan.

## Chiến lược Git/PR

PR #105 vẫn tồn tại, đang closed/unmerged, head branch còn truy cập được. Ưu tiên contributor cập nhật nhánh gốc bằng bản tích hợp đã review rồi maintainer mở lại PR #105 để giữ thảo luận và attribution. Không tự push lên fork của contributor, mở lại PR hoặc merge.

Nhánh local `feature/account-settings-completion` được chuẩn bị từ develop mới và merge toàn bộ lịch sử PR #105. Nếu dùng PR mới, push nhánh này vào fork của tài khoản hiện tại (`TQD-IT`, tạo fork nếu chưa có) sau khi được cho phép và tạo PR về `TieuBao-IT89/CookShare:develop`; không push trực tiếp main/develop. Chưa push, tạo/mở lại PR hoặc merge trên GitHub.
