# Security Policy – CookShare

## 1. Giới thiệu (Overview)

CookShare là dự án phần mềm mã nguồn mở được phát triển bằng ASP.NET Core MVC và C#.

Nhóm phát triển CookShare cam kết tiếp nhận và xử lý các báo cáo về lỗ hổng bảo mật một cách có trách nhiệm, nhằm bảo vệ mã nguồn, dữ liệu và người sử dụng hệ thống.

Tài liệu này hướng dẫn cách báo cáo, xác minh, khắc phục và công bố lỗ hổng bảo mật.

## 2. Các phiên bản được hỗ trợ (Supported Versions)

CookShare hiện đang trong quá trình phát triển.

| Phiên bản / Nhánh                             | Trạng thái                                             |
| --------------------------------------------- | ------------------------------------------------------ |
| `main`                                        | Nhánh ổn định, được ưu tiên xem xét các vấn đề bảo mật |
| `develop`                                     | Nhánh phát triển, tiếp nhận báo cáo và khắc phục lỗi   |
| Các phiên bản cũ hoặc nhánh không còn duy trì | Không cam kết hỗ trợ bảo mật                           |

Các nhánh trên không đại diện cho một lịch trình hỗ trợ dài hạn hoặc cam kết thời gian phát hành bản vá.

Khi có phiên bản phát hành chính thức, nhóm phát triển sẽ cập nhật chính sách hỗ trợ.

## 3. Báo cáo lỗ hổng bảo mật (Reporting a Vulnerability)

Nếu phát hiện lỗ hổng bảo mật trong CookShare, vui lòng báo cáo riêng tư cho Maintainer.

**Không công khai chi tiết lỗ hổng chưa được khắc phục trên GitHub Issues, Pull Requests hoặc Discussions.**

### Kênh báo cáo ưu tiên

Sử dụng chức năng **Private Vulnerability Reporting** của GitHub nếu đã được kích hoạt:

1. Truy cập repository CookShare.
2. Mở tab **Security**.
3. Chọn **Advisories**.
4. Chọn **Report a vulnerability** nếu tùy chọn này khả dụng.
5. Điền thông tin và gửi báo cáo riêng tư.

Repository:

https://github.com/TieuBao-IT89/CookShare

Nếu chức năng báo cáo riêng tư chưa khả dụng, vui lòng liên hệ Maintainer để được cung cấp kênh báo cáo an toàn trước khi gửi chi tiết kỹ thuật nhạy cảm.

## 4. Thông tin cần cung cấp (Report Information)

Một báo cáo bảo mật nên có:

- Mô tả lỗ hổng.
- Thành phần hoặc chức năng bị ảnh hưởng.
- Nhánh hoặc phiên bản liên quan.
- Các bước tái hiện lỗ hổng.
- Kết quả thực tế và kết quả mong đợi.
- Mức độ ảnh hưởng và rủi ro dự kiến.
- Hình ảnh, log hoặc bằng chứng nếu cần thiết.
- Đề xuất khắc phục nếu có.

Không gửi mật khẩu, API Keys, Access Tokens hoặc dữ liệu cá nhân thật khi không cần thiết.

## 5. Quy trình xử lý (Vulnerability Handling Process)

### Bước 1. Tiếp nhận (Receive)

Maintainer tiếp nhận báo cáo thông qua kênh riêng tư.

Thông tin lỗ hổng được giới hạn cho những thành viên có trách nhiệm xử lý.

### Bước 2. Xác minh (Verify)

Maintainer hoặc Core Developer kiểm tra:

- Lỗ hổng có tồn tại không.
- Có thể tái hiện trong môi trường kiểm thử không.
- Những chức năng và phiên bản nào bị ảnh hưởng.

### Bước 3. Đánh giá (Assess)

Nhóm phát triển đánh giá mức độ nghiêm trọng dựa trên:

- Khả năng bị khai thác.
- Mức độ ảnh hưởng đến dữ liệu và hệ thống.
- Phạm vi người dùng bị ảnh hưởng.
- Khả năng khắc phục.

Mức độ ưu tiên được xác định theo rủi ro thực tế.

### Bước 4. Phân công (Assign)

Maintainer phân công thành viên phù hợp để phân tích và khắc phục.

Đối với lỗ hổng nhạy cảm, quá trình phát triển bản vá cần được thực hiện trong phạm vi riêng tư phù hợp, tránh tiết lộ sớm thông tin có thể bị khai thác.

### Bước 5. Khắc phục (Fix)

Developer thực hiện:

- Xác định nguyên nhân gốc.
- Xây dựng bản vá.
- Kiểm tra tác động đến chức năng hiện tại.
- Hạn chế thay đổi không liên quan.

### Bước 6. Kiểm thử (Test)

Nhóm phát triển thực hiện các kiểm tra phù hợp:

- Unit Tests.
- Build và kiểm tra CI.
- Kiểm thử hồi quy.
- Kiểm tra lại khả năng tái hiện lỗ hổng.

Bản vá chỉ được chấp thuận sau khi đáp ứng các yêu cầu chất lượng phù hợp.

### Bước 7. Phát hành (Release)

Maintainer quyết định thời điểm tích hợp và phát hành bản vá, ưu tiên giảm thiểu rủi ro đối với người dùng.

Nếu đã có bản phát hành chính thức, hướng dẫn cập nhật cần chỉ rõ phiên bản được khắc phục.

### Bước 8. Công bố có trách nhiệm (Responsible Disclosure)

Sau khi đánh giá mức độ an toàn và tình trạng khắc phục, nhóm phát triển có thể công bố:

- Mô tả tổng quan lỗ hổng.
- Phạm vi ảnh hưởng.
- Mức độ nghiêm trọng.
- Phiên bản đã được khắc phục.
- Hướng dẫn cập nhật hoặc giảm thiểu rủi ro.

Việc công bố cần phối hợp với người báo cáo khi phù hợp.

## 6. Trách nhiệm của các thành viên (Responsibilities)

### Maintainer

- Quản lý kênh báo cáo bảo mật.
- Tiếp nhận và đánh giá báo cáo.
- Điều phối quá trình khắc phục.
- Quyết định phát hành và công bố thông tin.

### Core Developer

- Hỗ trợ xác minh lỗ hổng.
- Phân tích nguyên nhân và phát triển bản vá.
- Viết hoặc cập nhật kiểm thử.
- Đảm bảo thay đổi đáp ứng yêu cầu chất lượng.

### Contributor và Security Researcher

- Báo cáo qua kênh riêng tư.
- Cung cấp bằng chứng phù hợp.
- Tránh truy cập hoặc tiết lộ dữ liệu không được phép.
- Không khai thác lỗ hổng gây ảnh hưởng đến người dùng khác.
- Phối hợp công bố có trách nhiệm.

## 7. Nguyên tắc bảo mật (Security Best Practices)

Nhóm CookShare khuyến khích:

- Không hardcode mật khẩu, token hoặc API Keys.
- Sử dụng Environment Variables hoặc .NET User Secrets phù hợp.
- Không commit file chứa dữ liệu nhạy cảm.
- Kiểm tra dependencies và cảnh báo bảo mật.
- Thực hiện Code Review trước khi Merge.
- Sử dụng Unit Tests và GitHub Actions CI.
- Áp dụng nguyên tắc phân quyền tối thiểu.

## 8. Quản lý tài khoản (Account Management)

Người dùng đã đăng nhập quản lý tài khoản của chính mình tại `/Account/Settings`. Mỗi thao tác ghi dữ liệu dùng Authentication và Anti-Forgery. Người dùng không cập nhật được tài khoản của người khác.

- Đổi tên hiển thị chỉ thay `DisplayName`.
- Đổi email yêu cầu mật khẩu hiện tại. Địa chỉ cũ được giữ cho đến khi địa chỉ mới mở liên kết xác nhận.
- Đổi mật khẩu yêu cầu mật khẩu hiện tại. Security stamp được cập nhật và phiên đăng nhập được làm mới.
- Email xác minh và liên kết đặt lại mật khẩu hết hạn sau 1 giờ.
- Trang quên mật khẩu luôn trả cùng một thông báo, dù email có tài khoản hay không. Trang đó không hiển thị email, token hoặc mật khẩu.
- Token và mật khẩu không được ghi vào log hoặc thông báo thành công.

Dịch vụ email đọc cấu hình `Email`. `Email__SmtpPassword` và `Email__SmtpUser` được đặt bằng biến môi trường hoặc .NET User Secrets.

Khi người dùng xác nhận xóa tài khoản bằng mật khẩu hiện tại, CookShare xóa theo thứ tự:

1. Lượt thích, bình luận, đánh giá và công thức đã lưu của người đó.
2. Quan hệ theo dõi mà người đó là người theo dõi hoặc người được theo dõi.
3. Công thức do người đó đăng, cùng tương tác của người khác trên những công thức này.
4. Bản ghi ASP.NET Core Identity của tài khoản.

Các quan hệ này đang dùng `DeleteBehavior.Restrict`. Xóa dữ liệu phụ thuộc trước giúp không để lại khóa ngoại mồ côi và không chặn lệnh xóa tài khoản.

## 9. Liên hệ và hỗ trợ (Contact)

- Repository: https://github.com/TieuBao-IT89/CookShare
- Security: https://github.com/TieuBao-IT89/CookShare/security
- Maintainer: TieuBao-IT89

Vui lòng sử dụng kênh báo cáo riêng tư nếu đã được kích hoạt. Không gửi chi tiết khai thác lỗ hổng qua Issue công khai.

---

**Cảm ơn bạn đã góp phần nâng cao tính bảo mật của CookShare!**
