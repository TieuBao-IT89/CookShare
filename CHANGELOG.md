# Changelog

Thay đổi đáng chú ý của CookShare, theo [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) và [Semantic Versioning](https://semver.org/). Mục `Unreleased` mô tả mã đang tích hợp trên `develop`, chưa phải một bản phát hành.

## [Unreleased]

### Added

- Lưu công thức, tác giả và dữ liệu tương tác trong PostgreSQL qua EF Core; quản lý công thức theo quyền tác giả.
- Tài khoản ASP.NET Core Identity; xác minh/đổi email, đổi/khôi phục mật khẩu và xóa tài khoản kèm dữ liệu liên quan.
- Lưu công thức, thích, bình luận, theo dõi và đánh giá 1–5 sao theo tài khoản; thông báo trong ứng dụng theo người nhận.
- Bảng tin mới nhất/đang theo dõi, tải thêm bài và tương tác ngay trên feed.
- Hồ sơ người sáng tạo có Bio, ảnh bìa và tối đa ba công thức ghim.
- Seed công thức mẫu và import TheMealDB với kiểm tra trùng lặp.
- Nấu theo từng bước, nhớ vị trí trong phiên làm việc; gợi ý món nổi bật và công thức liên quan.
- Tải ảnh JPG/PNG/WebP tối đa 5 MiB, xem trước, lưu file ngoài web root và giữ ảnh khi sửa không chọn ảnh mới ([#129](https://github.com/TieuBao-IT89/CookShare/pull/129)).
- Khẩu phần gốc 1–20, điều chỉnh định lượng ở trang chi tiết mà không sửa dữ liệu gốc ([#130](https://github.com/TieuBao-IT89/CookShare/pull/130)).
- Thời gian chuẩn bị/nấu, độ khó và bộ lọc tương ứng; mở rộng mô tả ngay trên feed ([#131](https://github.com/TieuBao-IT89/CookShare/pull/131)).
- Triển khai Azure App Service Staging từ `develop`, health check và rollback gói ứng dụng.
- Hướng dẫn setup/testing, lưu trữ ảnh, thông tin nấu ăn, mục lục tài liệu, hỗ trợ và mẫu Issue/PR.

### Changed

- Cải thiện giao diện responsive, trạng thái form, thông báo và tương tác bàn phím.
- Tổ chức README và hướng dẫn cộng tác theo luồng bắt đầu → đóng góp → kiểm thử → vận hành; phân biệt hướng dẫn hiện tại với báo cáo lịch sử.
- CI dùng PostgreSQL tạm cho kiểm thử tích hợp, không dùng credential Neon của ứng dụng.

### Removed

- Module JavaScript/CSS và view tài khoản demo không còn dùng, Bootstrap chưa được tham chiếu, partial rỗng và test mẫu không có assertion.
- Báo cáo backend/database ban đầu, bộ ảnh UI #85, bản nháp PR và script kiểm thử localStorage đã lỗi thời; hướng dẫn hiện hành và bằng chứng kiểm chứng liên quan còn được giữ lại.

### Fixed

- Tích hợp khẩu phần và metadata nấu ăn trong controller, form, model, snapshot và mô hình đích của migration mới nhất; bổ sung test hồi quy create/edit cùng ảnh upload ([#131](https://github.com/TieuBao-IT89/CookShare/pull/131)).
- Sửa ngày và trạng thái bản v0.1.0 trong tài liệu theo Release đã công bố.

## [0.1.0] - 2026-10-19

Bản phát hành đầu tiên. Nội dung bên dưới ghi nhận phạm vi lịch sử của tag `v0.1.0`, không mô tả trạng thái hiện tại của `develop`.

### Added

- Nền tảng ASP.NET Core MVC/C#; giao diện trang chủ, khám phá, chi tiết và quản lý công thức.
- Giao diện tài khoản và hồ sơ; bình luận demo bằng localStorage và tương tác Like trên giao diện.
- Project xUnit, GitHub Actions build/test và các tài liệu README, Contributing, Code of Conduct, Security, Changelog.
- MIT License.

### Changed

- Chuẩn hóa quy tắc C# bằng `.editorconfig` và kiểm tra chất lượng trong CI.
- Thiết lập cộng tác qua Issue, PR và code review.

### Security

- Bỏ qua build output và cấu hình cá nhân trong Git; hướng dẫn quản lý secrets và báo cáo lỗ hổng riêng tư.

Ở bản v0.1.0, một số chức năng mới ở mức giao diện/demo và chưa tích hợp đầy đủ backend/database. Xem [Release notes v0.1.0](https://github.com/TieuBao-IT89/CookShare/releases/tag/v0.1.0) để biết phạm vi bản phát hành.

## Cập nhật changelog

Ghi thay đổi hướng người dùng vào `Unreleased` theo `Added`, `Changed`, `Deprecated`, `Removed`, `Fixed` hoặc `Security`; bỏ nhóm không có nội dung. Liên kết PR khi có và không ghi công việc dự định như đã hoàn thành. Khi phát hành, maintainer chuyển đúng phạm vi vào phiên bản/ngày thực tế theo [Releasing](RELEASING.md).

[Unreleased]: https://github.com/TieuBao-IT89/CookShare/compare/v0.1.0...develop
[0.1.0]: https://github.com/TieuBao-IT89/CookShare/releases/tag/v0.1.0
