# Cài đặt và chạy local

Chạy lệnh từ thư mục gốc chứa `CookShare.sln`. Hướng dẫn dành cho `develop`; với bản phát hành, đọc tài liệu tại tag tương ứng.

## Yêu cầu

| Thành phần | Vai trò |
| --- | --- |
| Git | Lấy mã nguồn và gửi đóng góp |
| .NET 8 SDK | Restore, build, chạy ứng dụng và test |
| PostgreSQL local hoặc Neon | Lưu tài khoản, công thức và tương tác |
| EF CLI 8.0.11 | Áp dụng migration, cùng phiên bản EF trong project |
| SMTP có TLS | Gửi email xác minh/khôi phục; tùy chọn khi khám phá ứng dụng local |

Visual Studio/VS Code là tùy chọn. Không cần Node.js để build. Python và Chrome/Edge chỉ cần cho frontend harness.

```bash
git clone --branch develop https://github.com/TieuBao-IT89/CookShare.git
cd CookShare
dotnet --version
dotnet restore CookShare.sln
dotnet tool install --global dotnet-ef --version 8.0.11
```

Nếu tool đã tồn tại, kiểm tra `dotnet ef --version`; dùng `dotnet tool update --global dotnet-ef --version 8.0.11` khi cần đồng bộ.

## Database

Tạo database và tài khoản PostgreSQL riêng cho phát triển. Với Neon, lấy kết nối của database/branch phát triển, giữ yêu cầu SSL của dịch vụ và dùng kết nối trực tiếp khi chạy migration.

Lưu kết nối bằng .NET User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=cookshare_dev;Username=cookshare_dev;Password=<your-local-password>" --project CookShare/CookShare.csproj
```

Thay thông tin bằng database thực của bạn. User Secrets nằm ngoài Git, chỉ được nạp mặc định trong Development. Không gửi giá trị thật vào Issue/PR.

Hoặc dùng biến môi trường trong PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__DefaultConnection = "<your-development-connection-string>"
```

Bash:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__DefaultConnection='<your-development-connection-string>'
```

Biến môi trường ghi đè cấu hình tương ứng từ file/User Secrets. `ConnectionStrings__DefaultConnection` map sang `ConnectionStrings:DefaultConnection`.

Áp dụng toàn bộ migration đã commit:

```bash
dotnet ef database update --project CookShare/CookShare.csproj -- --environment Development
```

Ứng dụng không tự chạy migration khi khởi động web. Chạy lại lệnh khi cập nhật mã có thay đổi schema; không dùng `EnsureCreated` thay cho migration.

## Chạy ứng dụng

```bash
dotnet build CookShare.sln
dotnet run --project CookShare/CookShare.csproj --launch-profile http
```

Mở **http://localhost:5051**. Đăng ký qua giao diện; database mới chưa có công thức. Xem [dữ liệu mẫu](RECIPE_SEEDING.md) nếu muốn seed. CLI seed chỉ chạy ở Development/Testing, không tự tạo tài khoản.

`GET /health` kiểm tra kết nối database. HTTP 200 ở endpoint này không thay thế kiểm tra schema, đăng nhập hoặc thao tác lưu công thức.

## Email

Ứng dụng cho phép đăng nhập khi chưa xác minh email. Cần SMTP để xác minh, đổi email và khôi phục mật khẩu. Cấu hình qua User Secrets hoặc biến môi trường:

| Biến môi trường | Nội dung |
| --- | --- |
| `Email__PublicBaseUrl` | URL gốc tin cậy; local có thể dùng `http://localhost:5051`, ngoài Development/Testing bắt buộc HTTPS |
| `Email__FromAddress` | Địa chỉ gửi |
| `Email__FromName` | Tên gửi; mặc định `CookShare` |
| `Email__SmtpHost` | Máy chủ SMTP |
| `Email__SmtpPort` | Cổng; mặc định 587 |
| `Email__SmtpUser` | Tài khoản SMTP |
| `Email__SmtpPassword` | Credential SMTP |
| `Email__UseStartTls` | `true`: STARTTLS; `false`: TLS ngay khi kết nối |
| `Email__TimeoutSeconds` | Thời gian chờ; mặc định 30 giây |

Cả hai chế độ SMTP đều yêu cầu mã hóa. Khi chưa cấu hình/gửi lỗi, xác minh hoặc đổi email báo chưa gửi được; quên mật khẩu giữ thông báo chung để không lộ tài khoản. Xem [Account settings](ACCOUNT_SETTINGS.md).

## Ảnh và triển khai

Ảnh upload mặc định nằm trong `CookShare/App_Data/recipe-images`, ngoài web root và không nằm trong gói publish. Cấu hình storage bền vững và sao lưu cùng database theo [Recipe images](RECIPE_IMAGES.md). Hướng dẫn Azure nằm trong [Deployment](DEPLOYMENT.md).

## Xử lý lỗi

| Triệu chứng | Kiểm tra |
| --- | --- |
| Không kết nối được PostgreSQL | Database, host/cổng, tài khoản, SSL và biến môi trường ghi đè User Secrets |
| Thiếu bảng/cột | Áp dụng migration của nhánh hiện tại vào đúng database Development |
| Không tìm thấy `dotnet ef` | Tool 8.0.11 và thư mục global tools trong `PATH` |
| Cổng 5051 đang dùng | Dừng phiên cũ hoặc dùng `--no-launch-profile --urls http://localhost:5052`; giữ môi trường Development nếu dùng User Secrets |
| Không gửi được email | SMTP/TLS, credential và `Email__PublicBaseUrl` |
| Upload lỗi/ảnh mất sau deploy | Quyền ghi, volume và `RecipeImages__StoragePath` |

Xem [Support](../SUPPORT.md) để báo lỗi, kèm log đã che thông tin nhạy cảm.
