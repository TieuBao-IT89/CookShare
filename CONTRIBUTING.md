# Đóng góp cho CookShare

Chào mừng các đóng góp về mã nguồn, kiểm thử, giao diện và tài liệu. Mọi thành viên tuân thủ [Code of Conduct](CODE_OF_CONDUCT.md). Báo cáo lỗ hổng theo [SECURITY.md](SECURITY.md), không qua Issue công khai.

## Trước khi bắt đầu

Tìm [Issue](https://github.com/TieuBao-IT89/CookShare/issues) và [PR](https://github.com/TieuBao-IT89/CookShare/pulls) hiện có để tránh làm trùng. Với thay đổi lớn, mô tả vấn đề và hướng xử lý trong Issue trước. Sửa lỗi nhỏ hoặc lỗi tài liệu có thể gửi PR trực tiếp và nêu rõ lý do.

Thiết lập môi trường theo [Setup](docs/SETUP.md). Cần Git, .NET 8 SDK và PostgreSQL khi chạy ứng dụng. Editor là lựa chọn cá nhân; tuân thủ [.editorconfig](.editorconfig).

## Nhánh và đồng bộ mã nguồn

| Nhánh | Mục đích |
| --- | --- |
| `main` | Mã ổn định dùng cho phát hành |
| `develop` | Tích hợp thay đổi đã review |
| `feature/<short-description>` | Tính năng mới |
| `fix/<short-description>` | Sửa lỗi |
| `docs/<short-description>` | Tài liệu |
| `test/<short-description>` | Kiểm thử |
| `security/<short-description>` | Bảo mật; xử lý riêng tư nếu chưa công bố |
| `chore/<short-description>` | Bảo trì và công cụ |

Dùng tên ngắn, chữ thường, phân cách từ bằng dấu `-`. Tạo nhánh từ `develop`, gửi PR vào `develop`; maintainer quản lý việc tích hợp sang `main` theo [Releasing](RELEASING.md).

Contributor bên ngoài fork repo rồi chạy:

```bash
git clone https://github.com/<your-username>/CookShare.git
cd CookShare
git remote add upstream https://github.com/TieuBao-IT89/CookShare.git
git fetch upstream
git switch develop
git merge --ff-only upstream/develop
git switch -c feature/recipe-search
```

Nếu clone trực tiếp repo gốc và có quyền ghi, dùng `origin` thay cho `upstream`; không thêm remote trùng. Nếu không thể fast-forward, kiểm tra các commit khác nhau trước khi tiếp tục; không reset hoặc force-push để bỏ qua vấn đề.

Đồng bộ nhánh công việc trước khi yêu cầu review:

```bash
git fetch upstream
git switch feature/recipe-search
git merge upstream/develop
```

Giải quyết xung đột bằng cách giữ hành vi của các tính năng liên quan và chạy lại kiểm tra. Không force-push nhánh dùng chung.

## Viết thay đổi

- Giữ mỗi PR tập trung vào một vấn đề. Không đưa build output, secrets, database dump hoặc định dạng hàng loạt không liên quan vào commit.
- C#: PascalCase cho kiểu/phương thức/property, interface bắt đầu bằng `I`, camelCase cho biến/tham số, `_camelCase` cho private field; thụt lề bốn dấu cách.
- Kiểm tra đầu vào, quyền sở hữu và CSRF ở server. Nội dung người dùng phải được hiển thị an toàn.
- Thay đổi hành vi cần kiểm thử phù hợp. UI cần kiểm tra desktop/mobile, bàn phím và trạng thái lỗi; xem [frontend harness](CookShare.Tests/Frontend/README.md).
- Thay đổi schema phải có migration, snapshot và mô hình đích tương ứng. Khi nhiều PR cùng thêm migration, kiểm tra thứ tự và bảo toàn các trường của migration trước.
- Cập nhật tài liệu gần tính năng và mục `Unreleased` trong [changelog](CHANGELOG.md) nếu người dùng bị ảnh hưởng.

Công cụ thiết kế UI là tùy chọn; xem [UI workflow](docs/UI_DESIGN_WORKFLOW.md). Chúng không phải điều kiện để build hoặc đóng góp.

## Kiểm tra trước PR

Chạy tại thư mục gốc repo:

```bash
dotnet restore CookShare.sln
dotnet build CookShare.sln --configuration Release --no-restore
dotnet test CookShare.sln --configuration Release --no-build --no-restore
dotnet format whitespace CookShare.sln --verify-no-changes --no-restore
dotnet format style CookShare.sln --verify-no-changes --no-restore --severity warn
dotnet format analyzers CookShare.sln --verify-no-changes --no-restore --severity warn
```

Test PostgreSQL không tự bật ở local. Xem [Testing](docs/TESTING.md) để dùng database cô lập và chạy tương đương CI. Ghi rõ các test bỏ qua hoặc chưa chạy trong PR.

Nếu thay đổi model EF, chạy thêm:

```bash
dotnet ef migrations has-pending-model-changes --project CookShare/CookShare.csproj
```

## Commit và gửi PR

Ưu tiên Conventional Commits: `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`, `ci:` hoặc `security:`; ví dụ `docs: clarify local database setup`.

```bash
git status
git add <files-related-to-your-change>
git commit -m "feat: add recipe search"
git push -u origin feature/recipe-search
```

Mở PR vào `TieuBao-IT89/CookShare:develop`. Mô tả vấn đề, hành vi sau thay đổi, Issue liên quan, cách kiểm tra và giới hạn; thêm ảnh trước/sau khi sửa UI. Chỉ dùng `Closes #...` khi PR giải quyết toàn bộ Issue. GitHub chỉ tự đóng Issue theo từ khóa khi thay đổi tới nhánh mặc định; maintainer cần kiểm tra trạng thái Issue sau khi tích hợp vào `develop`.

## Review và merge

Contributor sửa trên nhánh PR và trả lời các nhận xét. Maintainer kiểm tra phạm vi, quyền truy cập, migration, tương thích và test; giải quyết mọi yêu cầu sửa bắt buộc trước khi merge.

Chỉ merge khi không có lỗi nghiêm trọng đã biết và các kiểm tra CI bắt buộc thành công trên trạng thái tích hợp mới nhất. Tích hợp từng PR, cập nhật các PR phụ thuộc khi cần và kiểm tra lại sau khi giải quyết xung đột. Lịch sử hiện dùng merge commit; tiếp tục cách này để giữ commit và tác giả. Không bỏ qua branch protection bằng quyền admin.

## Viết tài liệu

Dùng Markdown UTF-8, tiêu đề rõ, liên kết tương đối cho file nội bộ và lệnh có thể chạy từ thư mục được chỉ định. Tài liệu dùng tiếng Việt; giữ nguyên tên API, cấu hình và lệnh. README là điểm bắt đầu; chi tiết nằm trong `docs/`, được liên kết từ [mục lục](docs/README.md).

Ghi ngày/phạm vi cho báo cáo kiểm chứng; số test lịch sử không đại diện cho nhánh hiện tại. Không tự đặt ngày phát hành, địa chỉ hỗ trợ hoặc cam kết thời gian phản hồi. Hỏi đáp và hỗ trợ theo [SUPPORT.md](SUPPORT.md).
