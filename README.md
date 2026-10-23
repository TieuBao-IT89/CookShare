# CookShare

CookShare là dự án website chia sẻ công thức nấu ăn được phát triển bằng **ASP.NET Core MVC và C#**.

Dự án hướng đến xây dựng một nền tảng giúp người dùng khám phá các món ăn, tìm kiếm công thức, xem hướng dẫn chế biến và chia sẻ kiến thức nấu ăn với cộng đồng.

CookShare được phát triển theo mô hình phần mềm mã nguồn mở (Open Source Software – OSS), khuyến khích Contributors tham gia đóng góp, sửa lỗi và cải tiến hệ thống.

## 1. Giới thiệu dự án (About CookShare)

### Mục tiêu

- Xây dựng nền tảng chia sẻ công thức nấu ăn trực tuyến.
- Giúp người dùng dễ dàng khám phá và tìm hiểu cách chế biến món ăn.
- Xây dựng giao diện dễ sử dụng và thân thiện.
- Tạo nền tảng mở rộng các chức năng tương tác cộng đồng.
- Áp dụng quy trình cộng tác và quản lý mã nguồn bằng Git/GitHub.

### Đối tượng sử dụng

- Người yêu thích nấu ăn.
- Người muốn tìm kiếm công thức và học chế biến món ăn.
- Người muốn chia sẻ kinh nghiệm nấu ăn.
- Developers và Contributors muốn tham gia phát triển dự án.

## 2. Các chức năng chính (Features)

### Các giao diện và chức năng đang được phát triển

- **Trang chủ:** Hiển thị giao diện khám phá công thức nấu ăn.
- **Công thức nấu ăn:** Giao diện danh sách, chi tiết, tạo và chỉnh sửa công thức.
- **Tài khoản:** Đăng ký, đăng nhập bằng username hoặc email, ghi nhớ đăng nhập và đăng xuất bằng ASP.NET Core Identity.
- **Hồ sơ:** Giao diện thông tin người dùng.
- **Tìm kiếm và danh mục:** Các chức năng khám phá công thức theo nhu cầu người dùng.

### Các chức năng định hướng phát triển

- Xác minh email, khôi phục mật khẩu và mở rộng quản lý tài khoản người dùng.
- Lưu trữ dữ liệu công thức trong cơ sở dữ liệu.
- Lưu công thức yêu thích.
- Bình luận và đánh giá công thức.
- Thích và theo dõi người dùng.
- Cải thiện tìm kiếm, lọc và sắp xếp công thức.
- Nâng cao chất lượng phần mềm bằng Unit Tests và CI/CD.

**Lưu ý:** Một số chức năng hiện có mới ở mức giao diện hoặc đang được hoàn thiện. Trạng thái triển khai thực tế được theo dõi tại GitHub Issues.

## 3. Công nghệ sử dụng (Tech Stack)

| Thành phần         | Công nghệ                       |
| ------------------ | ------------------------------- |
| Ngôn ngữ lập trình | C#                              |
| Framework          | ASP.NET Core 8                  |
| Kiến trúc web      | MVC (Model – View – Controller) |
| View Engine        | Razor                           |
| Giao diện          | HTML, CSS, JavaScript           |
| Thư viện giao diện | Bootstrap, jQuery               |
| Quản lý mã nguồn   | Git                             |
| Nền tảng cộng tác  | GitHub                          |

Các thư viện và công nghệ có thể tiếp tục được cập nhật trong quá trình phát triển.

## 4. Yêu cầu hệ thống (Prerequisites)

Trước khi cài đặt CookShare, cần chuẩn bị:

- .NET 8 SDK.
- Git.
- Visual Studio 2022 hoặc Visual Studio Code.
- Trình duyệt web như Chrome, Edge hoặc Firefox.

Kiểm tra các công cụ đã cài đặt:

```bash
dotnet --version
git --version
```

Đảm bảo máy tính đã cài đặt .NET 8 SDK.

## 5. Hướng dẫn cài đặt (Installation)

### Bước 1. Clone repository

```bash
git clone https://github.com/TieuBao-IT89/CookShare.git
```

### Bước 2. Di chuyển vào thư mục dự án

```bash
cd CookShare
```

### Bước 3. Chuyển sang nhánh phát triển (nếu cần)

```bash
git switch develop
```

### Bước 4. Khôi phục dependencies

```bash
dotnet restore CookShare.sln
```

### Bước 5. Build dự án

```bash
dotnet build CookShare.sln
```

Nếu quá trình build thành công, có thể tiếp tục chạy ứng dụng.

## 6. Cấu hình môi trường (Configuration)

CookShare sử dụng các tệp cấu hình của ASP.NET Core:

```text
CookShare/
├── appsettings.json
└── appsettings.Development.json
```

Các file này được sử dụng để khai báo cấu hình ứng dụng theo môi trường.

Khi cần sử dụng thông tin bí mật như mật khẩu hoặc chuỗi kết nối cơ sở dữ liệu, nên sử dụng Environment Variables hoặc .NET User Secrets trong môi trường phát triển.

Không commit mật khẩu, API Keys hoặc Access Tokens lên GitHub.

## 7. Cấu hình cơ sở dữ liệu (Database Setup)

CookShare sử dụng **PostgreSQL (Neon)** kết hợp với **Entity Framework Core 8** để quản lý và lưu trữ dữ liệu.

### Yêu cầu

Để cấu hình cơ sở dữ liệu, cần chuẩn bị:

- Tài khoản [Neon PostgreSQL](https://console.neon.tech).
- .NET 8 SDK.
- Công cụ Entity Framework Core CLI (`dotnet-ef`).

### Bước 1. Tạo cơ sở dữ liệu trên Neon

Truy cập [Neon Console](https://console.neon.tech) và tạo PostgreSQL Project nếu chưa có.

Tạo một database branch riêng dành cho môi trường phát triển.

Chọn database `neondb`, sau đó nhấn **Connect** để lấy thông tin kết nối.

Ưu tiên sử dụng kết nối trực tiếp khi chạy Migrations.

### Bước 2. Cấu hình Connection String

Tại thư mục gốc repository, chạy:

```bash
dotnet user-secrets init --project CookShare/CookShare.csproj
```

Cấu hình Connection String bằng .NET User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=YOUR_NEON_HOST;Port=5432;Database=neondb;Username=YOUR_USERNAME;Password=YOUR_PASSWORD;SSL Mode=Require" --project CookShare/CookShare.csproj
```

Thay `YOUR_NEON_HOST`, `YOUR_USERNAME` và `YOUR_PASSWORD` bằng thông tin thực tế từ Neon.

**Lưu ý:** Không commit mật khẩu hoặc Connection String chứa thông tin nhạy cảm lên GitHub. Tránh để mật khẩu trong lịch sử lệnh Terminal; nên sử dụng phương thức nhập bí mật an toàn.

### Bước 3. Kiểm tra Entity Framework Core

Kiểm tra phiên bản công cụ:

```bash
dotnet ef --version
```

Nếu chưa cài đặt, chạy:

```bash
dotnet tool install --global dotnet-ef --version 8.0.11
```

Sử dụng phiên bản tương thích với các thư viện Entity Framework Core của dự án.

### Bước 4. Cập nhật cơ sở dữ liệu

Tại thư mục gốc repository, chạy:

```bash
dotnet restore CookShare.sln
dotnet ef database update --project CookShare/CookShare.csproj
```

Nếu kết nối thành công, Entity Framework Core sẽ áp dụng các Migrations để tạo hoặc cập nhật bảng dữ liệu trong database.

Khi có thay đổi cấu trúc Entity, Contributors có thể tạo Migration mới:

```bash
dotnet ef migrations add MigrationName --project CookShare/CookShare.csproj
```

Không tạo lại Migration `InitialCreate` nếu đã tồn tại trong repository.

### Bước 5. Kiểm tra dữ liệu

Trong Neon Console, mở **SQL Editor** và chạy:

```sql
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY table_name;
```

Để kiểm tra dữ liệu công thức:

```sql
SELECT * FROM "Recipes";
```

Để kiểm tra lịch sử Migrations:

```sql
SELECT * FROM "__EFMigrationsHistory";
```

### Bước 6. Kiểm tra kết nối và Build dự án

Chạy:

```bash
dotnet build CookShare.sln
dotnet test CookShare.sln
```

Để xác nhận database hoạt động, cần kiểm tra thao tác đọc và ghi dữ liệu thông qua `ApplicationDbContext`.

**Lưu ý:** Nên sử dụng database branch riêng để phát triển và kiểm thử. Không chạy các Migrations thử nghiệm trực tiếp trên môi trường Production.

### Dữ liệu công thức mẫu

Xem [hướng dẫn Recipe Seeding](docs/RECIPE_SEEDING.md) để thêm 30 công thức mẫu cho các tài khoản kiểm thử hoặc import từ TheMealDB. Công cụ hỗ trợ Dry Run, chống trùng và chỉ chạy bằng lệnh riêng trong Development/Testing.

## 8. Hướng dẫn chạy dự án (Getting Started)

Sau khi lấy mã mới hoặc chuyển nhánh, áp dụng các migration còn thiếu vào database Development đã cấu hình trước khi mở website:

```bash
dotnet ef database update --project CookShare/CookShare.csproj -- --environment Development
```

Tại thư mục gốc repository, chạy:

```bash
dotnet run --project CookShare/CookShare.csproj
```

Sau khi ứng dụng khởi động thành công, Terminal sẽ hiển thị địa chỉ truy cập, ví dụ:

```text
Now listening on: http://localhost:xxxx
```

Mở địa chỉ được hiển thị để sử dụng ứng dụng.

Nhấn `Ctrl + C` trong Terminal để dừng ứng dụng.

### Xác thực người dùng (Authentication)

CookShare sử dụng **ASP.NET Core Identity** kết hợp với **Neon PostgreSQL** để quản lý tài khoản người dùng.

Các chức năng hỗ trợ:

- Đăng ký tài khoản.
- Đăng nhập bằng username hoặc email.
- Đăng xuất qua HTTP POST.
- Ghi nhớ đăng nhập.
- Bảo vệ chức năng yêu cầu xác thực: tạo công thức, công thức đã lưu và chỉnh sửa công thức; quyền truy cập chỉnh sửa được kiểm tra theo tác giả ở backend.

Thông tin tài khoản được lưu trong các bảng ASP.NET Core Identity trên PostgreSQL. Mật khẩu được xử lý bằng cơ chế Password Hashing của ASP.NET Core Identity, không lưu dưới dạng văn bản thuần.

Người dùng cần cấu hình database theo hướng dẫn **Database Setup** ở mục 7 trước khi sử dụng chức năng Authentication.

**Lưu ý:** Email Verification và Password Recovery chưa được triển khai trong phạm vi chức năng hiện tại. Trang tạo công thức hiện chỉ có giao diện; việc lưu công thức và kiểm tra quyền sở hữu khi cập nhật/xóa dữ liệu sẽ được hoàn thiện trong Issue #75.

## 9. Cấu trúc thư mục (Project Structure)

```text
CookShare/
├── CookShare/
│   ├── Controllers/
│   │   ├── HomeController.cs
│   │   ├── ProfileController.cs
│   │   └── RecipeController.cs
│   ├── Models/
│   │   ├── ErrorViewModel.cs
│   │   └── Recipe.cs
│   ├── Views/
│   │   ├── Home/
│   │   ├── Profile/
│   │   ├── Recipe/
│   │   └── Shared/
│   ├── wwwroot/
│   │   ├── css/
│   │   ├── img/
│   │   ├── js/
│   │   └── lib/
│   ├── Properties/
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── CookShare.csproj
├── .github/
│   └── workflows/
│       ├── deploy.yml
│       └── dotnet-ci.yml
├── docs/
│   └── DEPLOYMENT.md
├── .gitignore
├── CONTRIBUTING.md
├── LICENSE
├── README.md
└── CookShare.sln
```

**Giải thích:**

- `Controllers/`: Tiếp nhận và xử lý các yêu cầu HTTP.
- `Models/`: Định nghĩa các đối tượng dữ liệu.
- `Views/`: Chứa các giao diện Razor.
- `wwwroot/`: Chứa CSS, JavaScript, hình ảnh và thư viện frontend.
- `Program.cs`: Cấu hình và khởi động ứng dụng.
- `CookShare.csproj`: Cấu hình dự án .NET.
- `CookShare.sln`: Solution chứa dự án.
- `CONTRIBUTING.md`: Hướng dẫn đóng góp mã nguồn.
- `LICENSE`: Giấy phép mã nguồn mở MIT.

## 10. Hướng dẫn đóng góp (Contributing)

CookShare hoan nghênh các đóng góp từ cộng đồng.

Contributors có thể tham gia:

- Sửa lỗi.
- Phát triển tính năng.
- Cải thiện giao diện.
- Viết kiểm thử.
- Cập nhật tài liệu.
- Đề xuất cải tiến bảo mật.

Vui lòng đọc [CONTRIBUTING.md](CONTRIBUTING.md) để tìm hiểu quy trình đóng góp chi tiết.

## 11. Bộ quy tắc ứng xử (Code of Conduct)

CookShare cam kết xây dựng môi trường cộng tác thân thiện,
chuyên nghiệp và tôn trọng giữa Maintainer, Core Developers
và Contributors.

Khi tham gia tạo Issues, Pull Requests hoặc thực hiện
Code Review, mọi thành viên cần tuân thủ các quy tắc
giao tiếp và ứng xử của dự án.

Vui lòng đọc [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
để tìm hiểu các nguyên tắc ứng xử, hành vi không được
chấp nhận và quy trình báo cáo vi phạm.

## 12. Quy trình làm việc với Git/GitHub (Git Workflow)

Dự án sử dụng các nhánh:

- `main`: Mã nguồn ổn định.
- `develop`: Nhánh tích hợp trong quá trình phát triển.
- `feature/*`: Phát triển tính năng.
- `fix/*`: Sửa lỗi.
- `docs/*`: Cập nhật tài liệu.
- `test/*`: Bổ sung kiểm thử.

Quy trình đóng góp cơ bản:

1. Kiểm tra hoặc tạo GitHub Issue.
2. Fork và Clone repository nếu là Contributor bên ngoài.
3. Tạo nhánh mới từ `develop`.
4. Thực hiện thay đổi.
5. Commit và Push mã nguồn.
6. Tạo Pull Request hướng đến `develop`.
7. Maintainer hoặc Core Developer tiến hành Code Review.
8. Chỉnh sửa khi có Request Changes.
9. Merge Pull Request khi đáp ứng yêu cầu.

Xem các công việc tại [GitHub Issues](https://github.com/TieuBao-IT89/CookShare/issues).

## 13. Tích hợp liên tục (Continuous Integration - CI)

CookShare sử dụng **GitHub Actions** để tự động kiểm tra mã nguồn trong quá trình phát triển.

Workflow được cấu hình tại `.github/workflows/dotnet-ci.yml`.

### Khi nào CI được kích hoạt?

- Khi có commit được Push lên nhánh `main` hoặc `develop`.
- Khi tạo hoặc cập nhật Pull Request hướng đến `main` hoặc `develop`.

### Các bước kiểm tra tự động

1. Checkout mã nguồn từ repository.
2. Thiết lập môi trường .NET 8 SDK.
3. Khôi phục dependencies bằng `dotnet restore`.
4. Biên dịch Solution bằng `dotnet build`.
5. Chạy Unit Tests bằng `dotnet test`.

### Unit Tests

CookShare sử dụng **xUnit** để viết kiểm thử đơn vị.

Các bài kiểm thử được lưu trong project `CookShare.Tests`.

Để chạy toàn bộ Unit Tests:

```bash
dotnet test CookShare.sln
```

Để xem kết quả chi tiết:

```bash
dotnet test CookShare.sln --logger "console;verbosity=detailed"
```

GitHub Actions tự động chạy Unit Tests khi có Pull Request hướng đến `develop` hoặc `main`.

### Xem kết quả CI

Kết quả được hiển thị tại [GitHub Actions](https://github.com/TieuBao-IT89/CookShare/actions) hoặc tab **Checks** trong Pull Request.

CI giúp phát hiện lỗi biên dịch sớm và hỗ trợ Maintainer đánh giá chất lượng mã nguồn trước khi Merge.

## 14. Triển khai liên tục (Continuous Deployment)

CookShare triển khai bản đã được review trên nhánh `develop` lên **Azure App Service**, môi trường **Staging**.

Workflow nằm tại `.github/workflows/deploy.yml`. Hướng dẫn cấu hình secret, kiểm tra sau triển khai, triển khai thủ công và rollback nằm tại [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

### Khi nào CD chạy?

- Sau khi thay đổi trong ứng dụng hoặc bài kiểm thử được merge vào `develop`.
- Khi Maintainer chạy thủ công workflow **CookShare CD** và chọn nhánh `develop`.

Pull Request chưa merge không được triển khai. Workflow cũng từ chối mọi nhánh khác `develop`.

### Điều kiện để lần triển khai được chấp nhận

1. `dotnet build` và `dotnet test` thành công trong workflow.
2. Environment `staging` trên GitHub đã có secret của App Service và chuỗi kết nối database. Các giá trị này không được commit.
3. Sau khi triển khai, `/health` và trang chủ trả về HTTP 200.

Nếu kiểm tra sức khỏe thất bại, workflow triển khai lại gói của lần thành công gần nhất và đánh dấu lần chạy là thất bại. Có thể chạy lại bằng input `rollback` trên nhánh `develop`.

Bản phát hành chính thức trên `main` vẫn theo [RELEASING.md](RELEASING.md).

## 15. Chính sách bảo mật (Security Policy)

CookShare khuyến khích cộng đồng báo cáo các lỗ hổng bảo mật một cách có trách nhiệm.

Nếu phát hiện lỗ hổng, vui lòng không công khai chi tiết kỹ thuật trên GitHub Issues hoặc Pull Requests.

Xem hướng dẫn báo cáo và các quy trình xử lý tại [SECURITY.md](SECURITY.md).

Bạn có thể truy cập [GitHub Security](https://github.com/TieuBao-IT89/CookShare/security) để tìm hiểu các tùy chọn báo cáo bảo mật đang được hỗ trợ.

## 16. Giấy phép (License)

CookShare được phát hành theo giấy phép **MIT License**.

Chi tiết xem tại [LICENSE](LICENSE).

## 17. Lịch sử thay đổi (Changelog)

CookShare sử dụng tài liệu Changelog để ghi nhận các tính năng mới,
những cải tiến, sửa lỗi và cập nhật bảo mật trong quá trình phát triển.

Lịch sử thay đổi được tổ chức theo cấu trúc Keep a Changelog.

## Quản lý phiên bản và phát hành (Versioning & Releases)

CookShare sử dụng Semantic Versioning (`MAJOR.MINOR.PATCH`)
để quản lý các phiên bản phần mềm.

- `main`: Nhánh ổn định để phát hành.
- `develop`: Nhánh phát triển và tích hợp.
- Git Tags sử dụng định dạng `vX.Y.Z`.
- GitHub Releases ghi nhận các bản phát hành chính thức.

Trước mỗi Release, mã nguồn phải được Build, kiểm thử
và hoàn thành Code Review.

Xem quy trình chi tiết tại [RELEASING.md](RELEASING.md).

Lịch sử thay đổi: [CHANGELOG.md](CHANGELOG.md).

Các bản phát hành: [GitHub Releases](https://github.com/TieuBao-IT89/CookShare/releases).

## 18. Liên kết dự án

- [GitHub Repository](https://github.com/TieuBao-IT89/CookShare)
- [GitHub Issues](https://github.com/TieuBao-IT89/CookShare/issues)
- [Pull Requests](https://github.com/TieuBao-IT89/CookShare/pulls)
- [Contribution Guidelines](CONTRIBUTING.md)

---

**CookShare – Chia sẻ công thức, kết nối đam mê nấu ăn.**
