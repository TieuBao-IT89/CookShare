# feat: complete account settings and profile management

Base: `TieuBao-IT89/CookShare:develop`

Closes #101

CookShare còn thiếu phần quản lý bảo mật tài khoản sau khi PR #105 đóng mà chưa merge. Thay đổi tích hợp lại triển khai đó trên develop hiện tại: Settings dẫn sang Profile Edit, đổi mật khẩu có mật khẩu cũ, email chỉ đổi sau khi xác minh địa chỉ mới, khôi phục mật khẩu bằng token một giờ và xóa tài khoản có xác thực lại trong transaction.

Tái sử dụng PR #105 của Nguyên Hải (`haivo200c-lang`), giữ lịch sử commit và ảnh minh chứng gốc. Phần hoàn thiện bổ sung cấu hình URL email tin cậy/TLS, bảo toàn trạng thái xác minh khi reset mật khẩu, xử lý featured recipes khi xóa và kiểm thử relational cô lập, gồm cookie thật, CSRF, token tái sử dụng và rollback.

Chính sách xóa giữ như PR #105: xóa công thức của tài khoản và các tương tác trên chúng; giữ tài khoản khác và dữ liệu không liên quan. Chi tiết, cấu hình SMTP và phạm vi kiểm chứng: [docs/ACCOUNT_SETTINGS.md](ACCOUNT_SETTINGS.md).

Validation: restore và build Debug/Release thành công bằng .NET 8; full Release suite trên PostgreSQL 16.15 disposable và live Chrome đạt **495 passed, 0 failed, 0 skipped**. Whitespace, style và analyzer checks theo CONTRIBUTING đều đạt. Browser thực hiện 17 kiểm tra workflow/layout ở 360/768/1440 px. Giữ toàn bộ 11 test gốc Account Settings, thêm kiểm tra security/cookie, deletion graph/rollback, profile consistency và SMTP bắt buộc TLS.

Chuẩn hóa test host dùng data-protection key tạm, logging test và thứ tự migration cho test Favorites; CI dùng tên database disposable đáp ứng guard hiện có. Không nới assertion hoặc thêm bypass vào ứng dụng.

Clean build còn 7 cảnh báo cũ tại ApplicationDbContext/migrations và cấu hình documentation. SMTP thật, HTTPS/Azure deployment và GitHub Actions sau push chưa được xác minh. Không truy cập database Neon chung, không thêm migration. Cần đặt `Email__PublicBaseUrl` và cấu hình SMTP bằng biến môi trường/User Secrets trước khi kiểm tra gửi thư thật.

PR gốc: https://github.com/TieuBao-IT89/CookShare/pull/105

Issue: https://github.com/TieuBao-IT89/CookShare/issues/101
