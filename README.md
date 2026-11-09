# CookShare

[![CI](https://github.com/TieuBao-IT89/CookShare/actions/workflows/dotnet-ci.yml/badge.svg?branch=develop)](https://github.com/TieuBao-IT89/CookShare/actions/workflows/dotnet-ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Nền tảng chia sẻ công thức nấu ăn viết bằng **ASP.NET Core MVC, .NET 8 và PostgreSQL**. Khám phá món ăn, đăng công thức và kết nối với những người cùng thích nấu nướng.

> Tài liệu mô tả mã nguồn trên `develop`. `main` dành cho bản ổn định; xem [Releases](https://github.com/TieuBao-IT89/CookShare/releases) để chọn phiên bản đã phát hành.

## Tính năng

- Tạo, sửa và xóa công thức theo quyền tác giả; tải ảnh JPG/PNG/WebP và xem trước.
- Tìm kiếm theo từ khóa, danh mục, độ khó và tổng thời gian chuẩn bị/nấu.
- Điều chỉnh khẩu phần, tính lại nguyên liệu có định lượng và nấu theo từng bước.
- Bảng tin mới nhất/đang theo dõi, thích, bình luận, lưu công thức và đánh giá 1–5 sao.
- Hồ sơ có Bio, ảnh bìa, ghim tối đa ba công thức và theo dõi người sáng tạo.
- Đăng ký/đăng nhập, xác minh email, khôi phục mật khẩu, cài đặt và xóa tài khoản.
- Tài khoản, công thức và tương tác được lưu trong PostgreSQL; thông báo trong ứng dụng theo người nhận.

## Bắt đầu

Cần Git, **.NET 8 SDK** và một database **PostgreSQL** riêng (local hoặc Neon). Node.js không cần cho build ứng dụng.

```bash
git clone --branch develop https://github.com/TieuBao-IT89/CookShare.git
cd CookShare
dotnet restore CookShare.sln
dotnet tool install --global dotnet-ef --version 8.0.11
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=cookshare_dev;Username=cookshare_dev;Password=<your-local-password>" --project CookShare/CookShare.csproj
dotnet ef database update --project CookShare/CookShare.csproj -- --environment Development
dotnet run --project CookShare/CookShare.csproj --launch-profile http
```

Tạo database và thay thông tin kết nối bằng giá trị của bạn trước khi chạy. Nếu đã cài `dotnet-ef`, kiểm tra phiên bản bằng `dotnet ef --version`. Ứng dụng mở tại **http://localhost:5051**; đăng ký tài khoản để bắt đầu. Database mới chưa có công thức: xem [dữ liệu mẫu](docs/RECIPE_SEEDING.md).

Chi tiết môi trường, Neon, SMTP và xử lý lỗi nằm trong [hướng dẫn cài đặt](docs/SETUP.md). Không commit mật khẩu hoặc chuỗi kết nối.

## Kiểm thử

```bash
dotnet build CookShare.sln --configuration Release
dotnet test CookShare.sln --configuration Release --no-build
```

Các test cần PostgreSQL hoặc trình duyệt chạy theo cơ chế opt-in. [Hướng dẫn kiểm thử](docs/TESTING.md) giải thích cách bật chúng, kiểm tra định dạng và chạy tương đương CI.

## Tài liệu

| Bạn cần | Tài liệu |
| --- | --- |
| Cài đặt, cấu hình và chạy local | [Setup](docs/SETUP.md) |
| Kiểm thử và kiểm tra chất lượng | [Testing](docs/TESTING.md) |
| Kiến trúc dữ liệu và migration | [Database design](docs/DATABASE_DESIGN.md) |
| Tải ảnh, khẩu phần, thời gian và độ khó | [Ảnh công thức](docs/RECIPE_IMAGES.md) · [Thông tin nấu ăn](docs/RECIPE_COOKING.md) |
| Triển khai Staging và rollback | [Deployment](docs/DEPLOYMENT.md) |
| Các hướng dẫn tính năng và bằng chứng kiểm chứng | [Mục lục tài liệu](docs/README.md) |

## Đóng góp và hỗ trợ

Đọc [CONTRIBUTING.md](CONTRIBUTING.md) trước khi bắt đầu: nhánh công việc tạo từ `develop`, đặt tên theo loại thay đổi và gửi PR vào `develop`. Bộ quy tắc ứng xử nằm tại [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

Báo lỗi hoặc đặt câu hỏi theo [SUPPORT.md](SUPPORT.md). Lỗ hổng bảo mật cần được [báo cáo riêng tư](SECURITY.md).

## Phát hành và giấy phép

Xem [changelog](CHANGELOG.md), [quy trình phát hành](RELEASING.md) và [GitHub Releases](https://github.com/TieuBao-IT89/CookShare/releases). CookShare sử dụng [MIT License](LICENSE).
