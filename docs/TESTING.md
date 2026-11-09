# Kiểm thử và chất lượng mã nguồn

Chạy tại thư mục gốc repository. Cấu hình CI nằm tại [dotnet-ci.yml](../.github/workflows/dotnet-ci.yml); số test trong báo cáo cũ không đại diện cho nhánh hiện tại.

## Kiểm tra mặc định

```bash
dotnet restore CookShare.sln
dotnet build CookShare.sln --configuration Release --no-restore
dotnet test CookShare.sln --configuration Release --no-build --no-restore
```

`CookShare.Tests` dùng xUnit. Test cô lập dùng InMemory/SQLite, recording email sender và test authentication theo fixture. PostgreSQL và test trình duyệt đầy đủ yêu cầu opt-in; kiểm tra dòng `Skipped` trước khi kết luận đã chạy đủ.

## PostgreSQL tương đương CI

CI cấp PostgreSQL 16 tạm cho từng job, database `cookshare_test_ci`, không dùng credential Neon. Ở local, dùng PostgreSQL trên loopback và database riêng có tên bắt đầu bằng `cookshare_test_`.

Fixture có thể áp dụng migration, ghi/xóa dữ liệu và tạo/xóa database phụ. Tài khoản PostgreSQL cần quyền tạo database cho fixture Account Settings. Không dùng database ứng dụng hoặc Neon dùng chung.

Nếu có Docker, tạo service test local:

```bash
docker run --detach --name cookshare-tests --publish 127.0.0.1:55432:5432 --env POSTGRES_USER=cookshare_test --env POSTGRES_PASSWORD=cookshare_test --env POSTGRES_DB=cookshare_test_ci postgres:16-alpine
```

Chờ PostgreSQL sẵn sàng. Các credential mẫu chỉ dành cho service test local. Nếu container đã tồn tại, dùng `docker start cookshare-tests`.

PowerShell:

```powershell
$env:COOKSHARE_RUN_POSTGRES_TESTS = "1"
$env:COOKSHARE_TEST_CONNECTION = "Host=localhost;Port=55432;Database=cookshare_test_ci;Username=cookshare_test;Password=cookshare_test;SSL Mode=Disable"
dotnet test CookShare.sln --configuration Release --no-build --no-restore
Remove-Item Env:COOKSHARE_RUN_POSTGRES_TESTS, Env:COOKSHARE_TEST_CONNECTION
```

Bash:

```bash
COOKSHARE_RUN_POSTGRES_TESTS=1 COOKSHARE_TEST_CONNECTION='Host=localhost;Port=55432;Database=cookshare_test_ci;Username=cookshare_test;Password=cookshare_test;SSL Mode=Disable' dotnet test CookShare.sln --configuration Release --no-build --no-restore
```

Fixture áp dụng migration trước test; không cần tự tạo schema. Dừng service sau khi xong bằng `docker stop cookshare-tests`.

## Định dạng và analyzer

```bash
dotnet format whitespace CookShare.sln --verify-no-changes --no-restore
dotnet format style CookShare.sln --verify-no-changes --no-restore --severity warn
dotnet format analyzers CookShare.sln --verify-no-changes --no-restore --severity warn
```

Đây là các bước CI ngoài build/test. Khi sửa định dạng, giới hạn vào file liên quan và kiểm tra diff trước khi commit.

## Migration

```bash
dotnet ef migrations has-pending-model-changes --project CookShare/CookShare.csproj
dotnet ef migrations script --idempotent --project CookShare/CookShare.csproj --output artifacts/migration-review.sql
```

Tạo `artifacts/` trước khi xuất SQL nếu chưa có. Hai lệnh không áp dụng schema lên database. Script sinh thành công chưa chứng minh migration thực thi thành công: cần bộ test PostgreSQL để kiểm tra ràng buộc. Khi nhiều PR sửa schema, kiểm tra snapshot và mô hình đích `.Designer.cs` của migration mới nhất.

## Trình duyệt

[Frontend README](../CookShare.Tests/Frontend/README.md) liệt kê harness chạy JavaScript/CSS thực với phản hồi có kiểm soát. Cần Python và Chrome/Edge, không cần npm hoặc database ứng dụng. Chạy desktop/mobile cho phần UI thay đổi.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-RecipeServingScalerBrowserTests.ps1 -Viewport Desktop
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-RecipeServingScalerBrowserTests.ps1 -Viewport Mobile
```

Harness không thay thế kiểm tra toàn trang, đăng nhập thật hoặc SMTP. Để chạy browser Account Settings với form Razor và host SQLite cô lập:

```powershell
$env:COOKSHARE_RUN_BROWSER_TESTS = "1"
dotnet test CookShare.sln --configuration Release --no-build --filter FullyQualifiedName~AccountSettingsBrowserTests
Remove-Item Env:COOKSHARE_RUN_BROWSER_TESTS
```

Đặt `COOKSHARE_BROWSER_PATH` nếu cần. Xem giới hạn trong [Account settings](ACCOUNT_SETTINGS.md).

## Ghi kết quả trong PR

Nêu lệnh đã chạy, số test đạt/lỗi/bỏ qua, viewport và giới hạn chưa kiểm tra. Với lỗi sau tích hợp, thêm test hồi quy thể hiện hành vi người dùng. PR chỉ sửa tài liệu kiểm tra liên kết, nội dung lệnh và diff; CI vẫn chạy theo cấu hình repository.
