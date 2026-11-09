# Chính sách bảo mật

## Báo cáo lỗ hổng

Gửi báo cáo riêng tư qua [GitHub Security Advisories](https://github.com/TieuBao-IT89/CookShare/security/advisories/new). Repository đã bật Private Vulnerability Reporting. Nếu không truy cập được, kiểm tra [trang Security](https://github.com/TieuBao-IT89/CookShare/security) và liên hệ maintainer qua kênh được công bố trên [hồ sơ](https://github.com/TieuBao-IT89) để thống nhất kênh riêng tư trước khi gửi chi tiết.

Không công khai cách khai thác chưa được khắc phục trong Issue/PR. Không gửi mật khẩu, token, chuỗi kết nối thật hoặc dữ liệu cá nhân.

Báo cáo nên gồm:

- Nhánh/tag và commit bị ảnh hưởng.
- Thành phần, điều kiện và các bước tái hiện tối thiểu.
- Tác động, bằng chứng đã che dữ liệu nhạy cảm và đề xuất sửa nếu có.
- Cách liên hệ để phối hợp xác minh và công bố.

## Phạm vi hỗ trợ

| Mã nguồn | Chính sách |
| --- | --- |
| `main` | Tiếp nhận vấn đề bảo mật của nhánh ổn định |
| `develop` | Tiếp nhận và tích hợp bản sửa trong phát triển |
| Tag cũ và nhánh không còn duy trì | Không cam kết backport; maintainer đánh giá từng trường hợp |

Dự án chưa công bố lịch hỗ trợ dài hạn hoặc SLA phản hồi. Danh sách bản phát hành tại [Releases](https://github.com/TieuBao-IT89/CookShare/releases).

## Xử lý và công bố

Maintainer xác minh trong môi trường cô lập, đánh giá mức độ ảnh hưởng, phối hợp bản vá và test hồi quy. Thông tin khai thác chỉ chia sẻ với người cần xử lý. Thời điểm phát hành/công bố được thống nhất theo rủi ro thực tế; advisory nêu phiên bản bị ảnh hưởng và hướng cập nhật khi có kết luận.

## Cấu hình và dữ liệu

- Dùng User Secrets khi phát triển và biến môi trường/secret store khi triển khai. Không commit credential; xem [Setup](docs/SETUP.md).
- Thao tác tài khoản và công thức kiểm tra authentication, quyền sở hữu và anti-forgery tại server.
- Liên kết email dùng `Email__PublicBaseUrl` tin cậy, yêu cầu HTTPS ngoài Development/Testing. SMTP dùng TLS; token xác minh/đặt lại mật khẩu hết hạn sau một giờ.
- Quên mật khẩu trả thông báo chung. Đặt lại mật khẩu không tự đăng nhập hoặc tự xác minh email. Security stamp được cập nhật; các phiên khác bị vô hiệu khi Identity kiểm tra lại stamp, mặc định tối đa khoảng 30 phút.
- Xóa tài khoản yêu cầu mật khẩu hiện tại, xóa dữ liệu phụ thuộc trong transaction và đăng xuất phiên hiện tại. Chi tiết giữ/xóa từng bảng ở [Account settings](docs/ACCOUNT_SETTINGS.md).
- Ảnh công thức đọc công khai; file cũ chưa được tự dọn khi thay ảnh/xóa công thức/tài khoản. Xem [Recipe images](docs/RECIPE_IMAGES.md) để hiểu giới hạn lưu trữ.

Chỉ chạy test ghi/xóa trên database riêng theo [Testing](docs/TESTING.md). CI dùng PostgreSQL tạm, không dùng credential Neon của ứng dụng.
