# Kiểm chứng CookShare Social 2.0

Ngày chạy cuối: **14/11/2026**, nhánh `feature/cookshare-social-v2`, baseline `2d125b4c74013b07b883c6a62101487e54638a32`. Xem [phạm vi và migration](../../SOCIAL_V2.md), [audit tham khảo](../../SOCIAL_V2_UI_AUDIT.md) và [kết quả máy đọc](validation.json).

## Commit bàn giao

| Commit | Phạm vi |
| --- | --- |
| `c199ac5` | Schema, model, migration và backfill tìm kiếm |
| `ff5bfa3` | Query/nghiệp vụ, quyền, media, moderator và test backend |
| `1d506b2` | Razor/CSS/JS, browser harness và tài nguyên local có giấy phép |

Tài liệu và bằng chứng nằm trong commit kế tiếp. Ba phần ứng dụng được tách để review dữ liệu/nghiệp vụ độc lập với giao diện.

## Kết quả

| Kiểm tra | Kết quả |
| --- | --- |
| Restore / Release build / publish | Thành công; build có warning analyzer, gồm mã migration; không có error |
| `dotnet test CookShare.sln --configuration Release --no-restore` | **613 passed, 0 failed, 0 skipped**; bật cả `COOKSHARE_RUN_POSTGRES_TESTS=1` và `COOKSHARE_RUN_BROWSER_TESTS=1` |
| PostgreSQL mới và nâng cấp có dữ liệu | Đạt; test nâng cấp đối chiếu chín bảng cũ và backfill/index |
| `dotnet ef migrations has-pending-model-changes --project CookShare/CookShare.csproj --configuration Release --no-build` | Không có thay đổi model chưa có migration |
| `dotnet format whitespace/style/analyzers CookShare.sln --verify-no-changes --no-restore` | Cả ba exit 0; style/analyzers dùng `--severity warn` |
| Discovery browser harness | Desktop 15 / mobile 16 đạt |
| Home Feed browser harness | Desktop 30 / mobile 30 đạt |
| `git diff 2d125b4 --check` | Đạt |
| Luồng UI A/B/moderator trên app thật | [27 kiểm tra đạt](acceptance-results.json) |
| Dialog, upload lỗi, guest empty | [5 kiểm tra bổ sung đạt](feature-results.json), chạy với reduced motion |
| 7 route × 4 viewport × trước/sau | 56 trang HTTP 200, không cuộn ngang |

Lệnh restore/build/test và cách bật PostgreSQL/browser nằm trong [Testing](../../TESTING.md) và [Frontend harness](../../../CookShare.Tests/Frontend/README.md). Các log chạy đầy đủ nằm tại `artifacts/social-v2/` trong workspace, không đưa build output hay database vào Git. CI cần chạy lại khi maintainer tích hợp.

## Ảnh đối chiếu cùng fixture

Database demo riêng có 32 công thức, ảnh món lấy từ repository và ba tài khoản A/B/moderator. Hai bản publish chạy cùng database PostgreSQL đã nâng cấp: baseline chỉ dùng cột cũ; app mới dùng schema bổ sung. Trước khi chụp, xóa các tương tác của fixture rồi giữ nguyên giữa hai lượt. Cả hai đăng nhập A, locale `vi-VN`, scroll đầu trang; height 844 px cho 360/390 và 1000 px cho 768/1440.

Baseline giữ nguyên HTML/CSS/JS ở commit tham chiếu. Do CDN bị chặn trong môi trường review, trình duyệt phục vụ đúng byte Font Awesome/font đã xác minh cho baseline; app mới tự phục vụ local. Cách này cân bằng khả năng tải font, không sửa bố cục baseline. Khi chụp, chờ font và giải mã ảnh, kể cả ảnh lazy; một ảnh cố ý không tồn tại để kiểm tra fallback. File dưới đây chụp vùng nhìn đầu; ảnh full-page bổ sung nằm trong artifacts local. [Dữ liệu trước](before-results.json) / [sau](after-results.json).

| Màn hình | 360 px | 390 px | 768 px | 1440 px |
| --- | --- | --- | --- | --- |
| Bảng tin (`/`) | [Trước](before/home-360-viewport.png) · [Sau](after/home-360-viewport.png) | [Trước](before/home-390-viewport.png) · [Sau](after/home-390-viewport.png) | [Trước](before/home-768-viewport.png) · [Sau](after/home-768-viewport.png) | [Trước](before/home-1440-viewport.png) · [Sau](after/home-1440-viewport.png) |
| Khám phá (`/Recipe`) | [Trước](before/discovery-360-viewport.png) · [Sau](after/discovery-360-viewport.png) | [Trước](before/discovery-390-viewport.png) · [Sau](after/discovery-390-viewport.png) | [Trước](before/discovery-768-viewport.png) · [Sau](after/discovery-768-viewport.png) | [Trước](before/discovery-1440-viewport.png) · [Sau](after/discovery-1440-viewport.png) |
| Chi tiết món (`/Recipe/Details/1`) | [Trước](before/details-360-viewport.png) · [Sau](after/details-360-viewport.png) | [Trước](before/details-390-viewport.png) · [Sau](after/details-390-viewport.png) | [Trước](before/details-768-viewport.png) · [Sau](after/details-768-viewport.png) | [Trước](before/details-1440-viewport.png) · [Sau](after/details-1440-viewport.png) |
| Hồ sơ B (`/Profile?userId=social-b`) | [Trước](before/profile-360-viewport.png) · [Sau](after/profile-360-viewport.png) | [Trước](before/profile-390-viewport.png) · [Sau](after/profile-390-viewport.png) | [Trước](before/profile-768-viewport.png) · [Sau](after/profile-768-viewport.png) | [Trước](before/profile-1440-viewport.png) · [Sau](after/profile-1440-viewport.png) |
| Đăng món (`/Recipe/Create`) | [Trước](before/create-360-viewport.png) · [Sau](after/create-360-viewport.png) | [Trước](before/create-390-viewport.png) · [Sau](after/create-390-viewport.png) | [Trước](before/create-768-viewport.png) · [Sau](after/create-768-viewport.png) | [Trước](before/create-1440-viewport.png) · [Sau](after/create-1440-viewport.png) |
| Đã lưu (`/Recipe/Saved`) | [Trước](before/saved-360-viewport.png) · [Sau](after/saved-360-viewport.png) | [Trước](before/saved-390-viewport.png) · [Sau](after/saved-390-viewport.png) | [Trước](before/saved-768-viewport.png) · [Sau](after/saved-768-viewport.png) | [Trước](before/saved-1440-viewport.png) · [Sau](after/saved-1440-viewport.png) |
| Thông báo (`/Notifications`) | [Trước](before/notifications-360-viewport.png) · [Sau](after/notifications-360-viewport.png) | [Trước](before/notifications-390-viewport.png) · [Sau](after/notifications-390-viewport.png) | [Trước](before/notifications-768-viewport.png) · [Sau](after/notifications-768-viewport.png) | [Trước](before/notifications-1440-viewport.png) · [Sau](after/notifications-1440-viewport.png) |

Ở mobile, bài đầu tiên lên sớm hơn trên Feed; ảnh kết quả Khám phá xuất hiện trong vùng nhìn đầu. Nút Bắt đầu nấu trên chi tiết món ở trong màn hình đầu 390 px. Thanh dưới có safe area, toast được đẩy lên và cooking dialog ẩn thanh dưới. Desktop giữ bố cục feed ba cột và lưới khám phá bốn cột.

## Luồng tính năng mới

Ảnh dưới được chụp **sau** khi chạy nghiệm thu A/B/moderator, có dữ liệu mới; không dùng chúng làm cặp so sánh baseline.

| Luồng | Mobile 390 px | Desktop 1440 px |
| --- | --- | --- |
| Feed gồm công thức và thành phẩm | [Xem](features/mixed-feed-390.png) | [Xem](features/mixed-feed-1440.png) |
| Danh sách bộ sưu tập | [Xem](features/collections-390.png) | [Xem](features/collections-1440.png) |
| Chi tiết bộ | [Xem](features/collection-390.png) | [Xem](features/collection-1440.png) |
| Hộp chọn bộ | [Xem](features/collection-dialog-390.png) | [Xem](features/collection-dialog-1440.png) |
| Trang chọn bộ không phụ thuộc dialog | [Xem](features/native-chooser-390.png) | [Xem](features/native-chooser-1440.png) |
| Bài Đã nấu thử | [Xem](features/cooked-post-390.png) | [Xem](features/cooked-post-1440.png) |
| Đăng thành phẩm | [Xem](features/cooked-form-390.png) | [Xem](features/cooked-form-1440.png) |
| Hồ sơ có thành phẩm/bộ công khai | [Xem](features/cooked-profile-390.png) | [Xem](features/cooked-profile-1440.png) |
| Upload lỗi giữ nội dung | [Xem](features/upload-error-390.png) | [Xem](features/upload-error-1440.png) |

- [Hàng chờ kiểm duyệt](features/moderation-1440.png), [nhật ký ẩn/khôi phục](features/moderation-audit-1440.png).
- [Khách tìm không có kết quả](features/guest-empty-390.png).
- Luồng A/B kiểm tra bộ riêng trả 404, bộ công khai đọc được, Favorite độc lập, khẩu phần, cooking mode, ảnh thành phẩm và thông báo. B follow/unfollow A làm bài xuất hiện/biến mất ở Following. Moderator ẩn bài làm link bài và ảnh trực tiếp trả 404; khôi phục trả 200 và có hai audit.
- Dialog được kiểm tra Escape/trả focus ở 390 và 1440. Upload file giả bị từ chối và giữ ghi chú. Harness riêng kiểm tra sheet bộ lọc, mạng lỗi/retry, trạng thái pending, session thay đổi và nội dung tiếng Việt.

## Giới hạn còn mở

Safari/iOS và thiết bị vật lý chưa chạy trên Windows. Chưa đo tải production, dữ liệu hàng triệu dòng, SMTP thật, nhiều instance hay screen reader. Kiểm tra bàn phím/focus và các token tương phản không thay cho một chứng nhận WCAG toàn bộ ứng dụng. Upload xác minh signature/header/dimensions, chưa decode–reencode hoặc quét malware. Sort theo tương tác phản ánh điểm hiện tại; khi điểm đổi trong lúc phân trang, tải lại đầu danh sách để xem thứ tự mới. Các mục tủ lạnh, nháp server, dữ liệu nguyên liệu có cấu trúc, thử thách và realtime chỉ là phạm vi tiếp theo.

Không push, mở PR hoặc deploy trong lần bàn giao này. Maintainer cần review commit, chạy CI, backup database/media và áp dụng cấu hình vận hành trước khi phát hành.
