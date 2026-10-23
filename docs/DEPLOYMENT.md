# Hướng dẫn triển khai CookShare

## 1. Tổng quan

CookShare là ứng dụng ASP.NET Core MVC trên **.NET 8**. Tài liệu này mô tả cách đưa bản đã được review lên môi trường **Staging**, cách kiểm tra sau triển khai, cách triển khai thủ công khi workflow không dùng được, và cách khôi phục bản ổn định trước đó.

Workflow tự động nằm tại `.github/workflows/deploy.yml`.

## 2. Nền tảng

Môi trường Staging chạy trên **Azure App Service** với runtime **.NET 8**.

Nền tảng này nhận gói `dotnet publish`, dùng publish profile để xác thực, và để connection string nằm trong Application settings của App Service. GitHub Environment `staging` ghi lại mỗi lần triển khai trên GitHub Actions.

Bản phát hành chính thức vẫn đi theo nhánh `main` và quy trình trong [RELEASING.md](../RELEASING.md). Workflow này chỉ đưa bản đã merge vào `develop` lên Staging.

## 3. Môi trường Staging

Tạo một Azure App Service riêng cho Staging:

- Runtime stack: **.NET 8 (LTS)**.
- Hệ điều hành: **Linux**.
- HTTPS mặc định của `*.azurewebsites.net` được bật.

`AZURE_WEBAPP_NAME` là tên App Service, không gồm `https://` và không gồm `.azurewebsites.net`. Hostname công khai không phải lúc nào cũng là `<tên>.azurewebsites.net`. App tạo trên Azure Portal có thể dùng hostname dạng `<tên>-<hash>.<vùng>.azurewebsites.net`. Workflow lấy URL thật từ output `webapp-url` của action triển khai, không tự ghép từ tên app.

Trên GitHub, tạo Environment tên `staging` cho repository gốc:

1. Vào **Settings → Environments → New environment**.
2. Đặt tên `staging`.
3. Bật **Required reviewers** nếu muốn mỗi lần triển khai phải được Maintainer chấp nhận trước khi job deploy chạy.
4. Thêm các secret ở mục 7. Chỉ người có quyền quản trị repository mới thấy giá trị secret.

Workflow từ chối mọi nhánh khác `develop`. Pull Request chưa merge không được triển khai.

## 4. Điều kiện trước khi triển khai

- .NET 8 SDK trên máy người triển khai thủ công, hoặc runner `ubuntu-latest` của GitHub Actions.
- Nhánh `develop` đã qua Code Review và workflow CI `.github/workflows/dotnet-ci.yml` thành công.
- Database Staging trên Neon đã được tạo riêng, không dùng chung nhánh database phát triển.
- Ba secret ở mục 7 đã có trong Environment `staging`.
- Application settings ở mục 6 đã được nhập trên Azure.

## 5. Cơ sở dữ liệu

Ứng dụng dùng **PostgreSQL trên Neon** và **Entity Framework Core 8**. Các migration nằm trong `CookShare/Migrations`.

Chuẩn bị một Neon branch hoặc một database dành riêng cho Staging. Lấy chuỗi kết nối trực tiếp khi chạy migration.

Workflow deploy chạy lệnh sau trước khi đẩy gói mới lên Azure:

```bash
dotnet ef database update --project CookShare/CookShare.csproj --configuration Release
```

Lệnh này đọc chuỗi kết nối từ secret `STAGING_CONNECTION_STRING`. Giá trị được đưa vào biến môi trường `ConnectionStrings__DefaultConnection` và không được in ra log.

Migration trên Staging là thay đổi tiến tới. Job rollback của workflow chỉ khôi phục gói ứng dụng, không tự hạ schema. Nếu một migration không tương thích với gói cũ, khôi phục database bằng point-in-time restore hoặc một Neon branch đã tạo trước lần deploy đó.

Không chạy migration thử nghiệm trên database Staging hoặc Production.

## 6. Biến môi trường

Cấu hình các giá trị sau trong **Azure Portal → App Service → Settings → Environment variables**. Không ghi chúng vào `appsettings.json` hoặc commit vào repository.

| Tên | Giá trị |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Staging` |
| `ConnectionStrings__DefaultConnection` | Chuỗi kết nối Neon của database Staging. Cùng database với secret `STAGING_CONNECTION_STRING`. |

ASP.NET Core map `ConnectionStrings__DefaultConnection` thành `ConnectionStrings:DefaultConnection`.

Sau khi sửa Application settings, lưu thay đổi để App Service khởi động lại.

## 7. GitHub Actions Secrets

Thêm các secret sau vào Environment `staging`, không thêm vào mã nguồn:

| Secret | Nội dung |
| --- | --- |
| `AZURE_WEBAPP_NAME` | Tên App Service, không gồm `https://` và không gồm `.azurewebsites.net`. |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | Toàn bộ XML publish profile tải từ Azure Portal → App Service → **Get publish profile**. |
| `STAGING_CONNECTION_STRING` | Chuỗi kết nối PostgreSQL Staging, dùng để chạy `dotnet ef database update`. |

Publish profile và chuỗi kết nối là thông tin xác thực. GitHub Actions che các giá trị này trong log khi chúng được truyền qua `secrets`. Không dán chúng vào issue, pull request, hoặc tài liệu.

Nếu cả ba secret còn trống, job deploy bỏ qua bước gọi Azure và ghi chú trong summary. Thêm secret rồi chạy lại workflow để triển khai thật. Nếu chỉ thiếu một phần, job dừng với lỗi và không gọi Azure.

## 8. Quy trình tự động

Workflow **CookShare CD** chạy khi một trong hai điều kiện sau xảy ra:

- Có push vào `develop` và thay đổi nằm trong ứng dụng, bài kiểm thử, solution, hoặc chính file workflow.
- Maintainer chạy tay bằng **Actions → CookShare CD → Run workflow**, chọn nhánh `develop`.

Input `action` có hai giá trị:

- `deploy`: build, test, publish, migration, triển khai, rồi kiểm tra sức khỏe.
- `rollback`: tải gói đã được deploy thành công gần nhất và triển khai lại gói đó. Job này không chạy migration. Lần chạy chỉ xanh vì bỏ qua Azure không được chọn.

Thứ tự job `deploy`:

1. Job `guard` chỉ cho phép `refs/heads/develop`.
2. Job `publish` restore, build Release, chạy `dotnet test` với PostgreSQL tạm trên runner, rồi `dotnet publish`.
3. Gói zip được lưu thành artifact `cookshare-publish` trong 14 ngày.
4. Job `deploy` chỉ chạy khi job `publish` thành công.
5. Job áp dụng migration, triển khai zip lên App Service, rồi gọi `/health` và trang chủ trên URL mà action deploy trả về.
6. Nếu kiểm tra sức khỏe thất bại và còn một gói đã thực sự được deploy, workflow triển khai lại gói đó rồi vẫn đánh dấu lần chạy này là thất bại. Gói được chọn phải đến từ workflow này trên nhánh `develop`, và lần chạy đó phải có cả bước deploy lẫn health check thành công. Lần chạy chỉ xanh vì bỏ qua Azure khi thiếu secret không được dùng để rollback.

Hai lần triển khai Staging không chạy song song. Lần sau chờ lần trước kết thúc.

## 9. Kiểm tra sau triển khai

Endpoint `/health` trả về HTTP 200 khi tiến trình web khởi động và kết nối được database. Nội dung lỗi kết nối không được ghi vào phản hồi của endpoint này.

Workflow không tự ghép `https://<AZURE_WEBAPP_NAME>.azurewebsites.net`. Action `Azure/webapps-deploy` trả output `webapp-url`, và workflow gọi URL đó tối đa 18 lần, mỗi lần cách 10 giây:

- `<webapp-url>/health` phải trả HTTP 200.
- `<webapp-url>/` phải trả HTTP 200.

Cùng hai đường dẫn này được kiểm tra sau deploy, sau rollback tự động, và sau rollback chạy tay. Mở đúng URL ghi trong summary của lần chạy, hoặc hostname ở Azure Portal → App Service → Overview.

Sau khi workflow xanh, Maintainer nên kiểm tra thêm trên trình duyệt:

- Trang chủ mở được.
- Đăng ký hoặc đăng nhập bằng một tài khoản Staging.
- Mở danh sách công thức và một trang chi tiết.

## 10. Ghi nhận kết quả

Mỗi job deploy và rollback gắn với GitHub Environment `staging`. Kết quả xuất hiện tại:

- Tab **Actions** của repository, workflow **CookShare CD**.
- Mục **Deployments** của environment `staging`.

Job thành công ghi commit, URL và kết quả health check vào summary của lần chạy. Summary không chứa publish profile hoặc chuỗi kết nối.

## 11. Triển khai thủ công dự phòng

Dùng cách này khi GitHub Actions không chạy được nhưng App Service và database Staging vẫn sẵn sàng.

Tại thư mục gốc của bản `develop` đã được review:

```bash
dotnet restore CookShare.sln
dotnet build CookShare.sln --configuration Release
dotnet test CookShare.sln --configuration Release
dotnet tool install --global dotnet-ef --version 8.0.11
```

Áp dụng migration bằng chuỗi kết nối Staging. Nhập chuỗi qua biến môi trường của phiên làm việc, không ghi vào file:

```bash
dotnet ef database update --project CookShare/CookShare.csproj --configuration Release
```

Biến môi trường cần có trước lệnh trên:

```text
ConnectionStrings__DefaultConnection
ASPNETCORE_ENVIRONMENT=Staging
```

Đóng gói ứng dụng:

```bash
dotnet publish CookShare/CookShare.csproj --configuration Release --output ./publish
(cd publish && zip -qr ../cookshare.zip .)
```

`./publish` là thư mục. `az webapp deploy --type zip` cần đường dẫn tới file ZIP, không nhận thư mục đó.

Đẩy file `cookshare.zip` lên App Service bằng một trong hai cách:

- Azure Portal → App Service → **Deployment Center** hoặc **Advanced Tools**, dùng ZIP deploy của file vừa tạo.
- Azure CLI, sau khi đăng nhập bằng tài khoản có quyền trên resource group:

```bash
az webapp deploy --resource-group <resource-group> --name <app-name> --src-path ./cookshare.zip --type zip
```

Thay `<resource-group>` và `<app-name>` bằng giá trị của App Service Staging. Không commit file zip, publish profile, hoặc chuỗi kết nối.

Sau đó mở `/health` và trang chủ như mục 9.

## 12. Rollback

### Tự động khi health check thất bại

Nếu gói mới đã lên Azure nhưng `/health` hoặc trang chủ không trả HTTP 200, job deploy tìm artifact `cookshare-publish` mới nhất thỏa cả ba điều kiện: workflow là `.github/workflows/deploy.yml`, nhánh là `develop`, và job deploy của lần chạy đó đã triển khai thành công rồi vượt qua health check. Lần chạy xanh vì thiếu secret và bỏ qua Azure không được chọn. Sau khi triển khai lại gói cũ, workflow kiểm tra cả `/health` và trang chủ, rồi vẫn đánh dấu lần chạy này là thất bại.

Lần deploy thành công đầu tiên chưa có gói trước đó. Khi đó workflow không thể tự rollback.

### Chạy tay

1. Mở **Actions → CookShare CD → Run workflow**.
2. Chọn nhánh `develop`.
3. Chọn `action`: `rollback`.
4. Chờ job **Roll back Azure Staging**.
5. Xác nhận `/health` và trang chủ đều trả HTTP 200. URL là `webapp-url` trong summary, không ghép từ tên App Service.

Artifact chỉ được giữ 14 ngày. Ngoài khoảng đó, dùng Deployment Center của Azure để chọn một lần triển khai cũ hơn, hoặc publish lại commit đã biết là ổn định bằng mục 11.

### Database

Rollback ứng dụng không hoàn tác migration. Nếu schema mới làm bản cũ lỗi, khôi phục Neon về thời điểm trước migration rồi mới kiểm tra lại `/health`.

## 13. Xử lý sự cố

| Hiện tượng | Việc cần làm |
| --- | --- |
| Job publish thất bại | Đọc log build hoặc test. Sửa mã trên nhánh làm việc và merge lại vào `develop`. Chưa cần xem Azure. |
| Báo thiếu secret | Kiểm tra Environment `staging` đủ ba secret ở mục 7. Chạy lại workflow sau khi lưu secret. |
| `dotnet ef` thất bại | Kiểm tra chuỗi kết nối Staging, quyền user PostgreSQL, và việc database đã tồn tại. Không dán chuỗi kết nối vào issue. |
| Azure từ chối publish profile | Tải lại publish profile và cập nhật secret `AZURE_WEBAPP_PUBLISH_PROFILE`. Xác nhận `AZURE_WEBAPP_NAME` đúng tên App Service. |
| Health check hết số lần thử | Xem log của App Service. Kiểm tra Application settings ở mục 6 và runtime .NET 8. Nếu đã có gói cũ, xác nhận job đã triển khai lại gói đó hoặc chạy `rollback`. |
| Trang chủ sống nhưng đăng nhập lỗi | `/health` chỉ kiểm tra tiến trình và kết nối database. Kiểm tra migration và dữ liệu Identity trên database Staging. |
