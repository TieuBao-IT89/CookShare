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
- **Tài khoản:** Giao diện đăng nhập và đăng ký.
- **Hồ sơ:** Giao diện thông tin người dùng.
- **Tìm kiếm và danh mục:** Các chức năng khám phá công thức theo nhu cầu người dùng.

### Các chức năng định hướng phát triển

- Xác thực và quản lý tài khoản người dùng.
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

## 7. Hướng dẫn chạy dự án (Getting Started)

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

## 8. Cấu trúc thư mục (Project Structure)

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

## 9. Hướng dẫn đóng góp (Contributing)

CookShare hoan nghênh các đóng góp từ cộng đồng.

Contributors có thể tham gia:

- Sửa lỗi.
- Phát triển tính năng.
- Cải thiện giao diện.
- Viết kiểm thử.
- Cập nhật tài liệu.
- Đề xuất cải tiến bảo mật.

Vui lòng đọc [CONTRIBUTING.md](CONTRIBUTING.md) để tìm hiểu quy trình đóng góp chi tiết.

## 10. Quy trình làm việc với Git/GitHub (Git Workflow)

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

## 11. Tích hợp liên tục (Continuous Integration - CI)

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

## 12. Chính sách bảo mật (Security Policy)

CookShare khuyến khích cộng đồng báo cáo các lỗ hổng bảo mật một cách có trách nhiệm.

Nếu phát hiện lỗ hổng, vui lòng không công khai chi tiết kỹ thuật trên GitHub Issues hoặc Pull Requests.

Xem hướng dẫn báo cáo và quy trình xử lý tại [SECURITY.md](SECURITY.md).

Bạn có thể truy cập [GitHub Security](https://github.com/TieuBao-IT89/CookShare/security) để tìm hiểu các tùy chọn báo cáo bảo mật đang được hỗ trợ.

## 13. Giấy phép (License)

CookShare được phát hành theo giấy phép **MIT License**.

Chi tiết xem tại [LICENSE](LICENSE).

## 14. Liên kết dự án

- [GitHub Repository](https://github.com/TieuBao-IT89/CookShare)
- [GitHub Issues](https://github.com/TieuBao-IT89/CookShare/issues)
- [Pull Requests](https://github.com/TieuBao-IT89/CookShare/pulls)
- [Contribution Guidelines](CONTRIBUTING.md)

---

**CookShare – Chia sẻ công thức, kết nối đam mê nấu ăn.**
