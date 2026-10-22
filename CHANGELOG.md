# Changelog – CookShare

Tất cả những thay đổi đáng chú ý của dự án **CookShare** được ghi nhận trong tài liệu này.

Tài liệu được tổ chức theo [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) và sử dụng quy tắc đánh số phiên bản [Semantic Versioning](https://semver.org/).

CookShare đang trong giai đoạn phát triển ban đầu. Phiên bản `v0.1.0` đang được chuẩn bị để trở thành bản phát hành đầu tiên của dự án.

## [Unreleased]

Các thay đổi được thực hiện sau khi chốt phiên bản `v0.1.0` sẽ được ghi nhận tại đây.

### Added

- Thêm trang cài đặt tài khoản: cập nhật tên hiển thị, xác minh email, đổi email, đổi mật khẩu, quên mật khẩu và xóa tài khoản kèm dữ liệu liên quan.

## [0.1.0] - YYYY-MM-DD

**Initial Release – Phiên bản phát hành đầu tiên của CookShare (dự kiến)**

Phiên bản `0.1.0` tập trung xây dựng nền tảng website chia sẻ công thức nấu ăn, bổ sung các chức năng giao diện và thiết lập quy trình phát triển phần mềm mã nguồn mở.

### Added

**Core Features**

- Xây dựng website CookShare bằng ASP.NET Core MVC và C#.
- Bổ sung các giao diện trang chủ, danh sách công thức và chi tiết công thức.
- Bổ sung giao diện đăng nhập, đăng ký và hồ sơ người dùng.
- Bổ sung giao diện quản lý công thức nấu ăn.
- Thêm chức năng bình luận demo tại trang chi tiết công thức, sử dụng localStorage ([PR #61](https://github.com/TieuBao-IT89/CookShare/pull/61)).
- Bổ sung chức năng tương tác Like trên giao diện công thức ([PR #62](https://github.com/TieuBao-IT89/CookShare/pull/62)).

**Open Source Documentation**

- Bổ sung giấy phép mã nguồn mở MIT License ([PR #35](https://github.com/TieuBao-IT89/CookShare/pull/35)).
- Thêm tài liệu hướng dẫn đóng góp `CONTRIBUTING.md` ([PR #54](https://github.com/TieuBao-IT89/CookShare/pull/54)).
- Thêm bộ quy tắc ứng xử `CODE_OF_CONDUCT.md` ([PR #65](https://github.com/TieuBao-IT89/CookShare/pull/65)).
- Bổ sung tài liệu ghi nhận lịch sử thay đổi `CHANGELOG.md`.

**Testing & Continuous Integration**

- Thiết lập GitHub Actions CI cho quá trình Restore và Build .NET 8 ([PR #59](https://github.com/TieuBao-IT89/CookShare/pull/59)).
- Bổ sung project Unit Tests sử dụng xUnit ([PR #60](https://github.com/TieuBao-IT89/CookShare/pull/60)).
- Tích hợp kiểm thử tự động vào GitHub Actions.

### Changed

- Hoàn thiện `README.md` với phần giới thiệu, công nghệ sử dụng, hướng dẫn cài đặt và quy trình đóng góp ([PR #56](https://github.com/TieuBao-IT89/CookShare/pull/56)).
- Chuẩn hóa Coding Standards cho mã nguồn C# thông qua `.editorconfig`.
- Bổ sung các bước kiểm tra định dạng và chất lượng mã nguồn vào GitHub Actions ([PR #66](https://github.com/TieuBao-IT89/CookShare/pull/66)).
- Cải thiện quy trình cộng tác thông qua Issues, Pull Requests và Code Review.

### Security

- Cải thiện `.gitignore` và ngừng theo dõi các file build, cache và cấu hình cá nhân không cần thiết ([PR #55](https://github.com/TieuBao-IT89/CookShare/pull/55)).
- Bổ sung chính sách bảo mật `SECURITY.md`, hướng dẫn báo cáo và xử lý lỗ hổng bảo mật ([PR #63](https://github.com/TieuBao-IT89/CookShare/pull/63)).
- Bổ sung hướng dẫn quản lý cấu hình nhạy cảm và không công khai thông tin xác thực trong repository.

### Known Limitations

- Một số chức năng hiện mới được triển khai ở mức giao diện hoặc demo.
- Một số dữ liệu tương tác được lưu bằng localStorage và chưa đồng bộ giữa các thiết bị hoặc tài khoản.
- Các chức năng xác thực, lưu trữ dữ liệu và tương tác với cơ sở dữ liệu vẫn đang trong quá trình phát triển và hoàn thiện.
- Hệ thống kiểm thử hiện chưa bao phủ toàn bộ chức năng nghiệp vụ.

---

## Quy ước cập nhật Changelog

CookShare tuân thủ cấu trúc Keep a Changelog với các nhóm thay đổi:

| Nhóm       | Ý nghĩa                            |
| ---------- | ---------------------------------- |
| Added      | Chức năng hoặc thành phần mới      |
| Changed    | Các cải tiến và thay đổi           |
| Deprecated | Chức năng dự kiến ngừng hỗ trợ     |
| Removed    | Chức năng đã bị loại bỏ            |
| Fixed      | Các lỗi đã được khắc phục          |
| Security   | Các cập nhật liên quan đến bảo mật |

Khi thực hiện thay đổi đáng chú ý, Contributors cần:

1. Xác định nhóm thay đổi phù hợp.
2. Bổ sung mô tả ngắn gọn vào mục `Unreleased`.
3. Liên kết Issue hoặc Pull Request khi phù hợp.
4. Chỉ ghi nhận những thay đổi đã được triển khai và xác minh.
5. Không tự tạo phiên bản hoặc ngày phát hành chưa được xác nhận.
6. Khi phát hành phiên bản mới, Maintainer chuyển những thay đổi tương ứng từ `Unreleased` sang mục phiên bản chính thức.

## Quy tắc quản lý phiên bản

CookShare sử dụng Semantic Versioning:

`MAJOR.MINOR.PATCH`

Ví dụ:

- `v0.1.0`: Phiên bản phát triển đầu tiên.
- `v0.2.0`: Bổ sung chức năng mới.
- `v0.2.1`: Sửa lỗi của phiên bản trước.
- `v1.0.0`: Phiên bản được nhóm xác định có giao diện và chức năng công khai đủ ổn định.

Các phiên bản trên `v0.1.0` chỉ là ví dụ định hướng và chưa được công bố.

Git Tags được tạo từ commit phát hành trên nhánh `main`. GitHub Releases cung cấp Release Notes tương ứng với từng phiên bản.

## Liên kết

- [CookShare Repository](https://github.com/TieuBao-IT89/CookShare)
- [GitHub Releases](https://github.com/TieuBao-IT89/CookShare/releases)
- [GitHub Pull Requests](https://github.com/TieuBao-IT89/CookShare/pulls)
- [GitHub Issues](https://github.com/TieuBao-IT89/CookShare/issues)
- [Release Guidelines](RELEASING.md)

---

**CookShare – Chia sẻ công thức, kết nối đam mê nấu ăn.**
