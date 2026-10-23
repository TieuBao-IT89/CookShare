# Dữ liệu công thức mẫu và TheMealDB importer

Công cụ dành cho Development/Testing của [issue #113](https://github.com/TieuBao-IT89/CookShare/issues/113). Local Seed cung cấp 30 công thức do dự án chuẩn bị, có nội dung dài/ngắn và đủ chín danh mục hiện có. TheMealDB là nguồn import tùy chọn. Cả hai lưu Recipe vào PostgreSQL để website đọc trực tiếp, không gọi API khi người dùng mở trang.

**Sau khi chuyển sang nhánh này, cần cập nhật database trước khi mở website, kể cả khi chưa chạy seed.** Model Recipe sử dụng ba cột mới `ExternalId`, `Source`, `SourceUrl`; database cũ chưa có các cột này sẽ trả lỗi `42703: column r.ExternalId does not exist`.

Nếu đã cấu hình chuỗi kết nối Development trong User Secrets, chạy tại thư mục gốc repository:

```powershell
dotnet ef database update --project CookShare/CookShare.csproj -- --environment Development
dotnet run --project CookShare/CookShare.csproj
```

Migration chỉ cập nhật cấu trúc database. Thao tác thêm công thức mẫu vẫn cần chạy lệnh seed riêng ở bên dưới.

## Chuẩn bị database và tài khoản

1. Chọn database Development riêng hoặc một branch Neon dành riêng cho thử nghiệm. Môi trường ứng dụng không tự xác định được một chuỗi kết nối đang trỏ tới Production: cần kiểm tra database đích trước khi chạy.
2. Cấu hình `ConnectionStrings:DefaultConnection` bằng .NET User Secrets hoặc biến môi trường. Không commit chuỗi kết nối hay API key riêng.
3. Chọn môi trường `Development` hoặc `Testing`. CLI từ chối mọi môi trường khác, bao gồm `Production` và `Staging`, kể cả Dry Run.
4. Áp dụng migration `AddRecipeSourceMetadata` trước khi seed. Migration chỉ bổ sung metadata nullable, unique index và ràng buộc cặp nguồn/khóa, giữ nguyên Recipe đã có.
5. Tạo trước hai tài khoản `admintest1`, `root123` qua chức năng đăng ký trên database Development, hoặc chỉ định các username khác đã tồn tại. Seeder chỉ truy vấn Identity; không tạo tài khoản, đặt mật khẩu hay đổi tài khoản nhận thay thế.

Ví dụ PowerShell, chạy tại thư mục gốc repository:

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
$env:ASPNETCORE_ENVIRONMENT = "Development"

# Tự đặt biến này bằng chuỗi kết nối database Development riêng của bạn.
$env:ConnectionStrings__DefaultConnection = "<development-connection-string>"

dotnet restore CookShare.sln
dotnet ef database update --project CookShare/CookShare.csproj
```

Có thể dùng User Secrets thay cho biến chuỗi kết nối; xem hướng dẫn Database Setup trong README. Nếu dùng `Testing`, cấu hình connection qua biến môi trường vì User Secrets mặc định chỉ được nạp trong Development.

Các ví dụ bên dưới dùng `--no-launch-profile` để môi trường từ terminal được giữ nguyên, không bị launch profile của IDE đổi thành Development.

## Local Seed

Xem trước nội dung và tác giả của 30 công thức, không ghi database:

```powershell
dotnet run --project CookShare/CookShare.csproj --no-launch-profile -- seed-recipes --source local --count 30 --users admintest1,root123 --dry-run
```

Thực hiện import:

```powershell
dotnet run --project CookShare/CookShare.csproj --no-launch-profile -- seed-recipes --source local --count 30 --users admintest1,root123
```

Kết quả lần đầu khi database chưa có các khóa seed:

```text
Added: 30; skipped: 0; failed: 0; would add: 0.
```

Các công thức được phân lần lượt theo danh sách tài khoản: mặc định `admintest1` nhận 15, `root123` nhận 15. `UserId` được truy vấn từ `AspNetUsers.NormalizedUserName` qua Identity normalizer; không hardcode ID.

Chỉ thêm năm công thức đầu cho một tài khoản có sẵn:

```powershell
dotnet run --project CookShare/CookShare.csproj --no-launch-profile -- seed-recipes --source local --count 5 --users admintest1 --dry-run
```

Dữ liệu nằm trong `CookShare/Data/Seeds/sample-recipes.json` và được sao chép vào output/publish. Mỗi công thức có `key` ổn định, nội dung tiếng Việt và ảnh có sẵn trong `wwwroot` hoặc `/img/recipe-placeholder.svg`. Không dùng ảnh của món khác để minh họa những công thức chưa có ảnh phù hợp.

Khi bổ sung dữ liệu JSON, giữ nguyên `key` của công thức cũ. Khóa mới dài tối đa 128 ký tự; tên, mô tả, nguyên liệu, hướng dẫn và ảnh phải đáp ứng validation của form Recipe. Danh mục phải thuộc danh sách hiện có trong bộ lọc Explore.

## TheMealDB importer

Xem trước kết quả tìm kiếm theo tên:

```powershell
dotnet run --project CookShare/CookShare.csproj --no-launch-profile -- seed-recipes --source themealdb --search chicken --count 10 --users admintest1,root123 --dry-run
```

Nhập một công thức theo `idMeal`:

```powershell
dotnet run --project CookShare/CookShare.csproj --no-launch-profile -- seed-recipes --source themealdb --meal-id 52772 --count 1 --users admintest1
```

Nhập tối đa 30 ứng viên theo thứ tự tìm kiếm chữ cái:

```powershell
dotnet run --project CookShare/CookShare.csproj --no-launch-profile -- seed-recipes --source themealdb --count 30 --users admintest1,root123 --dry-run
```

Bỏ `--dry-run` sau khi đã kiểm tra dữ liệu và database đích để lưu thật.

Importer chỉ gọi các endpoint V1 chính thức:

- `search.php?s=...` khi có `--search`.
- `lookup.php?i=...` khi có `--meal-id`.
- `search.php?f=a` đến `search.php?f=z` khi không chỉ định tìm kiếm; dừng ngay khi đủ số ứng viên, tối đa 26 request.

Không gọi endpoint Premium, không scrape website và không chạy vòng lặp lấy món ngẫu nhiên. Mỗi request có timeout 20 giây và giới hạn response 4 MiB. Nếu một request HTTP/JSON thất bại, đợt đọc nguồn dừng trước khi ghi Recipe. Không có retry tự động.

API key thử nghiệm mặc định là `1`. Nếu cần key riêng, đặt `TheMealDb__ApiKey` trong môi trường hoặc `TheMealDb:ApiKey` trong User Secrets. HttpClient này tắt logger request mặc định để URL chứa key không xuất hiện trong log.

### Mapping

| TheMealDB | CookShare |
|---|---|
| `idMeal` | `ExternalId`, với `Source = themealdb` |
| `strMeal` | `Name` |
| `strMealThumb` | `Image`; dùng placeholder nếu URL không hợp lệ |
| `strCategory` | `Category` sau khi ánh xạ |
| `strInstructions` | `Instructions`, chuẩn hóa xuống dòng |
| `strIngredient1..20`, `strMeasure1..20` | Mỗi nguyên liệu một dòng, ghép số đo nếu có |
| `strSource` | `SourceUrl` nếu là URL HTTP(S) hợp lệ, nếu không dùng link công thức TheMealDB |
| `strMeal`, `strCategory`, `strArea` | Mô tả ngắn từ dữ liệu thực, giữ nguyên ngôn ngữ nguồn |

Nguyên liệu trống bị bỏ qua; nguyên liệu có tên nhưng không có số đo vẫn được giữ. Không suy đoán số lượng hay dịch nội dung. `Dessert` → `Món tráng miệng`; `Side`/`Starter` → `Món phụ`; `Vegan`/`Vegetarian` → `Món chay`; `Breakfast` → `Ăn sáng`; các danh mục món chính đã biết → `Món chính`. Danh mục lạ được báo lỗi để bổ sung mapping rõ ràng trước khi nhập.

Trang chi tiết hiển thị link ghi công TheMealDB và link nguồn gốc hợp lệ. Metadata nguồn không phải trường chỉnh sửa của form Recipe.

### Nguồn và điều khoản

- Tài liệu API: https://www.themealdb.com/api.php
- Điều khoản: https://www.themealdb.com/terms_of_use.php

Theo tài liệu TheMealDB, test key `1` dành cho phát triển/giáo dục; phát hành ứng dụng lên app store yêu cầu gói hỗ trợ trả phí. Chỉ sử dụng endpoint chính thức, giữ ghi công và thông báo bản quyền. Rà soát điều khoản hiện hành và quyền đối với nội dung bên thứ ba trước khi phát hành công khai. Các ràng buộc Development/Testing của công cụ vẫn áp dụng dù dùng key riêng.

## Tham số và báo cáo

| Tham số | Mặc định | Ý nghĩa |
|---|---|---|
| `--source` | `local` | `local` hoặc `themealdb` |
| `--count` | `30` | Giới hạn 1–100 ứng viên từ nguồn; Local hiện có 30 |
| `--users` | `admintest1,root123` | Các username Identity có sẵn, phân lần lượt |
| `--dry-run` | Tắt | Đọc tài khoản/khóa đã có, in JSON xem trước, không ghi dữ liệu |
| `--search` | Không có | Tìm TheMealDB theo tên; không dùng cùng `--meal-id` |
| `--meal-id` | Không có | Lấy chi tiết một TheMealDB ID |
| `--help` | — | Hiển thị hướng dẫn, không truy cập database |

`--count` giới hạn ứng viên được xét, không cam kết thêm đủ số Recipe mới. Ứng viên trùng hoặc lỗi không được thay bằng ứng viên khác. Nguồn ít hơn số yêu cầu sẽ thêm số thực có; nguồn rỗng báo thất bại. Tăng số lượng hoặc đổi từ khóa để chọn thêm công thức.

CLI trả exit code `0` khi không có công thức lỗi, kể cả khi tất cả đều đã tồn tại. Exit code `1` khi tham số, môi trường, tài khoản, nguồn hoặc database có lỗi. `Added`, `Skipped`, `Failed`, `Would add` cho biết kết quả; lỗi theo từng công thức được in kèm. Khi có cả ứng viên hợp lệ và không hợp lệ, các ứng viên hợp lệ vẫn được lưu và CLI trả `1` để yêu cầu kiểm tra báo cáo.

## Chống trùng và xử lý lỗi

- Khóa chống trùng toàn cục là `(Source, ExternalId)`, không dựa trên tên hay tài khoản nhận. Local dùng `key` từ JSON; TheMealDB dùng `idMeal`.
- Chạy lại hoặc đổi thứ tự `--users` không ghi đè nội dung, tác giả hoặc thời gian của Recipe đã nhập.
- Unique index trong PostgreSQL chặn hai tiến trình nhập cùng khóa. Tất cả Recipe hợp lệ mới của một lần chạy được lưu bằng một `SaveChangesAsync`, trong transaction của EF Core.
- Nếu database từ chối batch, toàn bộ batch được rollback và báo không có Recipe mới được thêm. Sau lỗi chạy đồng thời, chạy lại để kiểm tra các khóa đã được tiến trình khác nhập thành công.
- Nếu bị ngắt kết nối/hủy trong lúc commit, công cụ không khẳng định trạng thái commit. Chạy lại với `--dry-run` để đối chiếu database trước khi thử tiếp.
- Không có thao tác xóa, sửa Recipe cũ, thay đổi tài khoản hay tạo tương tác giả. Seeder không được gọi khi website khởi động và không có HTTP endpoint.
- Dry Run cần database đã được migration và tài khoản có sẵn; đây không phải chế độ chạy offline.

## Kiểm tra sau import

Trên database Development đã chọn:

```sql
SELECT r."Source", u."UserName", COUNT(*)
FROM "Recipes" r
JOIN "AspNetUsers" u ON u."Id" = r."UserId"
WHERE r."Source" IN ('local', 'themealdb')
GROUP BY r."Source", u."UserName"
ORDER BY r."Source", u."UserName";

SELECT "Source", "ExternalId", COUNT(*)
FROM "Recipes"
WHERE "Source" IS NOT NULL
GROUP BY "Source", "ExternalId"
HAVING COUNT(*) > 1;
```

Truy vấn đầu cho thấy phân bổ tác giả; truy vấn thứ hai phải không có hàng nào. Sau đó kiểm tra Home (mục mới nhất), Explore, tìm kiếm, bộ lọc danh mục, Profile của từng tác giả và Recipe Details. Home chỉ hiển thị món nổi bật/yêu thích/đánh giá cao khi có tương tác thật; seed không tự tạo Like hay Rating.

Thử Like, Comment, Favorites bằng tài khoản Development rồi tải lại trang. Chạy lại lệnh Local Seed: kết quả mong đợi là `Added: 0; skipped: 30; failed: 0` khi đã nhập đủ 30 khóa.

## Kiểm thử tự động

```powershell
dotnet build CookShare.sln --configuration Release
dotnet test CookShare.sln --configuration Release --no-build
```

Unit tests và HTTP integration tests dùng database InMemory riêng, HttpMessageHandler giả cho API, không kết nối Neon hay TheMealDB thật. Kiểm tra dữ liệu mẫu, mapping, giới hạn import, timeout/HTTP/JSON lỗi, tài khoản thiếu, Dry Run, chặn môi trường, chạy lại, hiển thị trang và tương tác trên Recipe đã seed.

Để chạy thêm kiểm thử transaction và unique index bằng PostgreSQL, chuẩn bị database trống tên `cookshare_test` hoặc `cookshare_test_*`, sau đó đặt biến chỉ trong terminal kiểm thử:

```powershell
$env:COOKSHARE_RUN_POSTGRES_TESTS = "1"
$env:COOKSHARE_TEST_CONNECTION = "<disposable-test-database-connection>"
dotnet test CookShare.sln --configuration Release
```

Fixture PostgreSQL áp dụng migration vào database kiểm thử. Các bài seeding dùng tài khoản ngẫu nhiên, xác nhận lưu qua nhiều DbContext, chống trùng khi hai tiến trình seed đồng thời và dọn đúng dữ liệu do bài kiểm thử tạo. GitHub Actions cung cấp PostgreSQL 16 riêng cho mỗi job và chạy các kiểm thử này, không dùng secret Neon.
