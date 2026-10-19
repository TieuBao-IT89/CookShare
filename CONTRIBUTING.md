# Hướng dẫn đóng góp cho CookShare (Contributing Guidelines)

Cảm ơn bạn đã quan tâm đến dự án **CookShare – Nền tảng chia sẻ công thức nấu ăn**!

CookShare là dự án mã nguồn mở khuyến khích cộng đồng tham gia phát triển, cải tiến chức năng, sửa lỗi và hoàn thiện tài liệu.

Tài liệu này hướng dẫn quy trình đóng góp mã nguồn nhằm đảm bảo tính nhất quán và chất lượng của dự án.

## 1. Các hình thức đóng góp

Bạn có thể đóng góp cho CookShare thông qua:

- Phát hiện và báo cáo lỗi (Bug Reports).
- Đề xuất tính năng mới (Feature Requests).
- Sửa lỗi và cải tiến chức năng.
- Cải thiện giao diện và trải nghiệm người dùng.
- Viết Unit Tests và nâng cao chất lượng mã nguồn.
- Cập nhật README và các tài liệu hướng dẫn.
- Đề xuất cải tiến bảo mật.

## 2. Yêu cầu môi trường phát triển

Để tham gia phát triển CookShare, bạn cần chuẩn bị:

- Git.
- Visual Studio 2022 hoặc Visual Studio Code.
- .NET 8 SDK.
- Tài khoản GitHub.

Kiểm tra môi trường bằng các lệnh:

```bash
git --version
dotnet --version
```

## 3. Fork và Clone Repository

### Bước 1. Fork repository

Truy cập:

https://github.com/TieuBao-IT89/CookShare

Nhấn **Fork** để tạo bản sao repository trên tài khoản GitHub cá nhân.

### Bước 2. Clone repository

```bash
git clone https://github.com/<username>/CookShare.git
cd CookShare
```

Thay `<username>` bằng tên tài khoản GitHub của bạn.

### Bước 3. Thêm Upstream

```bash
git remote add upstream https://github.com/TieuBao-IT89/CookShare.git
```

Kiểm tra:

```bash
git remote -v
```

- `origin`: repository đã Fork của Contributor.
- `upstream`: repository gốc của CookShare.

## 4. Cài đặt và chạy dự án

Tại thư mục gốc repository, thực hiện:

```bash
dotnet restore CookShare.sln
dotnet build CookShare.sln
dotnet run --project CookShare/CookShare.csproj
```

Mở địa chỉ localhost được hiển thị trong Terminal để truy cập ứng dụng.

Không đưa mật khẩu, API Key hoặc thông tin bí mật vào mã nguồn khi cấu hình môi trường.

## 5. Quy trình tạo Issue

Trước khi sửa lỗi hoặc phát triển tính năng, Contributor nên kiểm tra danh sách Issues hiện có để tránh trùng lặp.

Truy cập:

https://github.com/TieuBao-IT89/CookShare/issues

Khi tạo Issue, cần cung cấp:

- Tiêu đề rõ ràng.
- Mô tả vấn đề hoặc tính năng.
- Các bước tái hiện lỗi (nếu có).
- Kết quả hiện tại và kết quả mong đợi.
- Hình ảnh hoặc thông tin bổ sung nếu cần.

Ưu tiên trao đổi với Maintainer trước khi triển khai những thay đổi lớn.

## 6. Quy tắc quản lý Branch

CookShare sử dụng các nhánh chính:

- `main`: mã nguồn ổn định.
- `develop`: nhánh tích hợp các thay đổi trong quá trình phát triển.

Contributors cần tạo nhánh riêng từ `develop` để thực hiện công việc.

Quy tắc đặt tên:

| Loại công việc | Tên Branch               |
| -------------- | ------------------------ |
| Tính năng mới  | `feature/ten-tinh-nang`  |
| Sửa lỗi        | `fix/ten-loi`            |
| Tài liệu       | `docs/ten-tai-lieu`      |
| Kiểm thử       | `test/ten-kiem-thu`      |
| Bảo mật        | `security/ten-cong-viec` |
| Bảo trì        | `chore/ten-cong-viec`    |

Ví dụ:

```bash
git fetch upstream
git switch develop
git merge --ff-only upstream/develop
git switch -c feature/recipe-search
```

Nếu nhánh `develop` đang có thay đổi riêng chưa được đồng bộ, cần xử lý trước khi thực hiện fast-forward merge.

## 7. Quy tắc Commit Message

Sử dụng Commit Message ngắn gọn, rõ ràng, khuyến khích theo Conventional Commits.

Các tiền tố đề xuất:

- `feat:` Thêm tính năng mới.
- `fix:` Sửa lỗi.
- `docs:` Cập nhật tài liệu.
- `test:` Thêm hoặc cập nhật kiểm thử.
- `refactor:` Tái cấu trúc mã nguồn.
- `chore:` Công việc bảo trì.
- `ci:` Cập nhật CI/CD.
- `security:` Cải thiện bảo mật.

Ví dụ:

```bash
git commit -m "feat: add recipe search"
git commit -m "fix: resolve login validation issue"
git commit -m "docs: update contributing guidelines"
```

## 8. Quy tắc Coding Convention

### Coding Standards

CookShare sử dụng file `.editorconfig` để thống nhất quy tắc định dạng và phong cách viết mã C#.

Các Contributors cần tuân thủ:

- Class, Method và Property sử dụng PascalCase.
- Interface sử dụng tiền tố `I` kết hợp PascalCase.
- Biến cục bộ và tham số sử dụng camelCase.
- Private fields ưu tiên `_camelCase`.
- Sử dụng 4 dấu cách để thụt lề trong C#.
- Sử dụng dấu ngoặc `{ }` cho các khối điều kiện.
- Không để khoảng trắng không cần thiết cuối dòng.
- Không commit mã nguồn chưa được kiểm tra.

### Kiểm tra chất lượng mã nguồn

Trước khi tạo Pull Request, chạy:

```bash
dotnet restore CookShare.sln
dotnet format whitespace CookShare.sln --verify-no-changes
dotnet format style CookShare.sln --verify-no-changes --severity warn
dotnet format analyzers CookShare.sln --verify-no-changes --severity warn
dotnet build CookShare.sln
dotnet test CookShare.sln
```

Nếu kiểm tra thất bại, Contributor cần xem thông báo, sửa lỗi hoặc trao đổi với Maintainer trước khi gửi Pull Request.

Việc chỉnh sửa định dạng hàng loạt phải được kiểm tra kỹ để tránh đưa các thay đổi không liên quan vào PR.

## 9. Quy trình tạo Pull Request

Sau khi hoàn thành công việc:

### Bước 1. Kiểm tra thay đổi

```bash
git status
dotnet build CookShare.sln
```

Nếu đã có Unit Tests:

```bash
dotnet test CookShare.sln
```

### Bước 2. Commit thay đổi

```bash
git add .
git commit -m "feat: describe your change"
```

Chỉ đưa vào commit những file liên quan đến công việc.

### Bước 3. Push Branch

```bash
git push -u origin feature/recipe-search
```

Thay tên branch bằng branch đang thực hiện.

### Bước 4. Tạo Pull Request

Truy cập repository đã Fork, chọn **Compare & pull request**.

Thiết lập:

- Base repository: `TieuBao-IT89/CookShare`.
- Base branch: `develop`.
- Head repository: repository của Contributor.
- Compare branch: branch chứa thay đổi.

Pull Request cần mô tả:

- Mục đích thay đổi.
- Những chức năng hoặc file đã chỉnh sửa.
- Cách kiểm tra kết quả.
- Issue liên quan.

## 10. Quy trình Code Review

Maintainer hoặc Core Developer sẽ kiểm tra Pull Request dựa trên:

- Tính chính xác của chức năng.
- Chất lượng và khả năng đọc hiểu mã nguồn.
- Khả năng tương thích với dự án.
- Kết quả Build và kiểm thử.
- Mức độ tuân thủ Coding Convention.

Các kết quả Review có thể bao gồm:

**Approve:** Thay đổi đáp ứng yêu cầu và được chấp thuận.

**Request Changes:** Contributor cần chỉnh sửa theo nhận xét của Reviewer.

**Comment:** Reviewer đưa ra nhận xét hoặc đề xuất mà không yêu cầu phê duyệt hay từ chối trực tiếp.

Khi nhận Request Changes, Contributor cần:

1. Đọc nhận xét của Reviewer.
2. Chỉnh sửa trên Branch hiện tại.
3. Commit và Push các thay đổi mới.
4. Phản hồi những nhận xét đã xử lý.
5. Yêu cầu Review lại khi hoàn thành.

## 11. Quy trình Merge

Pull Request được xem xét Merge khi:

- Thay đổi đáp ứng yêu cầu của Issue.
- Không còn lỗi nghiêm trọng đã được phát hiện.
- Đã giải quyết các yêu cầu chỉnh sửa bắt buộc.
- Đáp ứng các kiểm tra CI bắt buộc nếu dự án đã cấu hình.
- Được người có thẩm quyền của repository chấp thuận.

Các thay đổi thông thường được Merge vào `develop`. Maintainer quản lý việc tích hợp các phiên bản ổn định từ `develop` sang `main`.

## 12. Đồng bộ Repository

Để cập nhật mã nguồn mới nhất từ repository gốc:

```bash
git fetch upstream
git switch develop
git merge --ff-only upstream/develop
git push origin develop
```

Nếu Git báo không thể fast-forward, cần kiểm tra sự khác biệt giữa các nhánh và giải quyết trước khi tiếp tục.

## 13. Quy tắc cộng đồng và bảo mật

Các thành viên cần:

- Giao tiếp lịch sự, tôn trọng ý kiến khác biệt.
- Đóng góp và phản hồi mang tính xây dựng.
- Không công kích cá nhân trong Issue hoặc Pull Request.
- Không công khai mật khẩu, token hoặc dữ liệu nhạy cảm.
- Không công khai chi tiết lỗ hổng có thể bị khai thác trước khi được xử lý.

Quy tắc ứng xử và quy trình báo cáo bảo mật sẽ được hướng dẫn chi tiết trong `CODE_OF_CONDUCT.md` và `SECURITY.md` khi được bổ sung.

## 14. Liên hệ và hỗ trợ

Nếu gặp khó khăn trong quá trình đóng góp, bạn có thể:

- Tạo Issue trên GitHub.
- Thảo luận trong Pull Request liên quan.
- Liên hệ Maintainer thông qua các kênh liên hệ công khai của dự án.

**Cảm ơn bạn đã đóng góp cho CookShare!**
