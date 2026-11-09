# Tài liệu CookShare

Tài liệu ở nhánh `develop` đi cùng mã nguồn đang tích hợp. Với phiên bản đã phát hành, đọc tài liệu tại tag tương ứng trong [Releases](https://github.com/TieuBao-IT89/CookShare/releases).

## Bắt đầu và vận hành

| Tài liệu | Nội dung |
| --- | --- |
| [Setup](SETUP.md) | Yêu cầu, database, cấu hình, SMTP và chạy local |
| [Testing](TESTING.md) | Unit/integration/browser tests, PostgreSQL cô lập và CI |
| [Database design](DATABASE_DESIGN.md) | Entity, ràng buộc, quan hệ và migration |
| [Deployment](DEPLOYMENT.md) | Azure Staging, health check và rollback |
| [Releasing](../RELEASING.md) | Phiên bản, tag và phát hành từ `main` |

## Tính năng

| Tài liệu | Nội dung |
| --- | --- |
| [Ảnh công thức](RECIPE_IMAGES.md) | Upload, kiểm tra tệp, vị trí lưu và sao lưu |
| [Thông tin nấu ăn](RECIPE_COOKING.md) | Khẩu phần, định lượng, thời gian và độ khó |
| [Dữ liệu mẫu](RECIPE_SEEDING.md) | Seed local và import TheMealDB |
| [Bảng tin](HOME_FEED.md) | Mới nhất/đang theo dõi, phân trang và tương tác |
| [Công thức đã lưu](PERSISTENT_FAVORITES.md) | Lưu/bỏ lưu theo tài khoản |
| [Tương tác](PERSISTENT_INTERACTIONS.md) | Thích, bình luận và dữ liệu PostgreSQL |
| [Đánh giá](RECIPE_RATINGS.md) | Quy tắc đánh giá 1–5 sao |
| [Hồ sơ người sáng tạo](CREATOR_PROFILE.md) | Bio, ảnh bìa và công thức ghim |
| [Cài đặt tài khoản](ACCOUNT_SETTINGS.md) | Email, mật khẩu và chính sách xóa dữ liệu |

## Đóng góp

- [Contributing](../CONTRIBUTING.md), [Code of Conduct](../CODE_OF_CONDUCT.md), [Security](../SECURITY.md) và [Support](../SUPPORT.md).
- [Frontend harness](../CookShare.Tests/Frontend/README.md) và [công cụ thiết kế UI tùy chọn](UI_DESIGN_WORKFLOW.md).
- [Changelog](../CHANGELOG.md) ghi thay đổi đáng chú ý; không dùng số kiểm thử trong báo cáo cũ để suy ra kết quả hiện tại.

## Báo cáo và bằng chứng lịch sử

Các tài liệu dưới đây ghi lại một đợt triển khai hoặc kiểm chứng cụ thể. Đọc cùng hướng dẫn tính năng hiện tại và mã nguồn; các checklist cũ không phải danh sách việc còn lại của dự án.

- [Tích hợp database ban đầu](RECIPE_DATABASE_INTEGRATION.md).
- [Backend audit](BACKEND_AUDIT.md).
- [UI audit #120](UI_AUDIT_120.md).
- [Bằng chứng UI](ui/README.md), [hồ sơ #117](ui/issue117/README.md), [cải tiến #120](ui/issue120/README.md).

## Nguồn tham khảo về cách tổ chức

Cấu trúc tài liệu và mẫu cộng tác tham khảo [GitHub contributor guidelines](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/setting-guidelines-for-repository-contributors), [GitHub templates](https://docs.github.com/en/communities/using-templates-to-encourage-useful-issues-and-pull-requests/about-issue-and-pull-request-templates) và cách tách hướng dẫn trong [ASP.NET Core](https://github.com/dotnet/aspnetcore/blob/main/CONTRIBUTING.md). Quy tắc nhánh của CookShare được giữ theo [CONTRIBUTING.md](../CONTRIBUTING.md).
