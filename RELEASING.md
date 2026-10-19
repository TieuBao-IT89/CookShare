# Release Process – CookShare

## 1. Giới thiệu (Overview)

Tài liệu này mô tả quy trình quản lý phiên bản và phát hành phần mềm của dự án CookShare.

Mục tiêu là đảm bảo mỗi bản phát hành được kiểm tra, có số phiên bản rõ ràng và có tài liệu mô tả các thay đổi.

## 2. Quy tắc đánh số phiên bản (Semantic Versioning)

CookShare áp dụng [Semantic Versioning](https://semver.org/) với cấu trúc:

`MAJOR.MINOR.PATCH`

Trong đó:

- **MAJOR:** Tăng khi có thay đổi không tương thích ngược với giao diện hoặc hợp đồng sử dụng đã công bố.
- **MINOR:** Tăng khi bổ sung chức năng tương thích ngược.
- **PATCH:** Tăng khi sửa lỗi tương thích ngược.

Ví dụ:

| Phiên bản | Ý nghĩa                                                                        |
| --------- | ------------------------------------------------------------------------------ |
| `0.1.0`   | Phiên bản phát triển ban đầu, nếu được chọn để phát hành                       |
| `0.2.0`   | Bản phát triển tiếp theo có chức năng mới                                      |
| `0.2.1`   | Bản sửa lỗi cho phiên bản 0.2.0                                                |
| `1.0.0`   | Phiên bản đầu tiên có hợp đồng sử dụng công khai được nhóm xác định là ổn định |

Trong giai đoạn `0.x.y`, dự án vẫn đang phát triển và có thể thay đổi giao diện hoặc chức năng chưa ổn định.

Các số phiên bản trên chỉ là ví dụ; không đại diện cho Release đã tồn tại của CookShare.

## 3. Quy tắc Git Tags

Mỗi phiên bản chính thức sử dụng Git Tag với tiền tố `v`.

Ví dụ:

- `v0.1.0`
- `v0.2.0`
- `v0.2.1`
- `v1.0.0`

Git Tag phải trỏ đến commit đã được phát hành trên nhánh `main`.

Ưu tiên sử dụng Annotated Tags để ghi nhận thông tin phiên bản.

Không tự ý di chuyển, ghi đè hoặc xóa Tag đã được công bố.

## 4. Vai trò các nhánh

- `main`: Chứa mã nguồn ổn định được sử dụng để tạo Release.
- `develop`: Nhánh tích hợp các thay đổi trong quá trình phát triển.
- `feature/*`: Phát triển tính năng.
- `fix/*`: Sửa lỗi.
- `docs/*`: Cập nhật tài liệu.
- `test/*`: Bổ sung kiểm thử.

Thông thường, Contributors gửi Pull Request vào `develop`. Maintainer chịu trách nhiệm quyết định khi nào mã nguồn đủ điều kiện đưa vào `main`.

## 5. Điều kiện phát hành (Release Criteria)

Trước khi tạo Release, cần đảm bảo:

- Các tính năng dự kiến phát hành đã hoàn thành.
- Không còn lỗi nghiêm trọng chưa được xử lý.
- Các Pull Request liên quan đã được Review.
- Build thành công.
- Unit Tests thành công.
- GitHub Actions CI đạt yêu cầu.
- Các kiểm tra Code Quality bắt buộc thành công.
- Không có thông tin nhạy cảm bị đưa vào bản phát hành.
- `CHANGELOG.md` được cập nhật.
- Số phiên bản và nội dung Release Notes đã được Maintainer phê duyệt.

## 6. Quy trình phát hành (Release Workflow)

### Bước 1. Hoàn thành công việc trên develop

Các thay đổi được tích hợp vào `develop` thông qua Pull Requests đã được review.

### Bước 2. Kiểm tra chất lượng

Chạy:

```bash
dotnet restore CookShare.sln
dotnet build CookShare.sln --configuration Release
dotnet test CookShare.sln --configuration Release --no-build
```

Kiểm tra trạng thái GitHub Actions trước khi phát hành.

### Bước 3. Chuẩn bị Changelog

Cập nhật `CHANGELOG.md` dựa trên những thay đổi thực sự nằm trong phạm vi phát hành.

Chuyển các mục tương ứng từ `Unreleased` sang tiêu đề phiên bản đã chọn, kèm ngày phát hành thực tế.

### Bước 4. Đưa phiên bản ổn định lên main

Tạo Pull Request từ `develop` sang `main`.

Maintainer kiểm tra sự khác biệt, xác nhận CI và tiến hành Merge nếu đáp ứng các điều kiện phát hành.

### Bước 5. Tạo Git Tag

Sau khi mã nguồn phát hành đã có trên `main`, chạy:

```bash
git switch main
git pull --ff-only origin main
git tag -a vX.Y.Z -m "Release vX.Y.Z"
git push origin vX.Y.Z
```

Thay `vX.Y.Z` bằng số phiên bản được nhóm phê duyệt.

### Bước 6. Tạo GitHub Release

Truy cập:

https://github.com/TieuBao-IT89/CookShare/releases

Chọn **Draft a new release**, sau đó:

1. Chọn Git Tag vừa tạo.
2. Nhập tiêu đề phiên bản.
3. Viết Release Notes.
4. Kiểm tra nội dung và phạm vi mã nguồn.
5. Nhấn Publish release sau khi được xác nhận.

### Bước 7. Kiểm tra sau phát hành

- Kiểm tra Release xuất hiện trên GitHub.
- Xác nhận Tag trỏ đến commit dự kiến.
- Kiểm tra đường dẫn tải Source code.
- Kiểm tra Release Notes và CHANGELOG.
- Thông báo phiên bản đến Contributors nếu cần.

## 7. Mẫu Release Notes

```markdown
# CookShare vX.Y.Z

## Highlights

Mô tả ngắn gọn những điểm nổi bật của phiên bản.

## Added

- Các tính năng mới.

## Changed

- Những cải tiến quan trọng.

## Fixed

- Các lỗi đã được khắc phục.

## Security

- Các cập nhật bảo mật đã được công bố.

## Known Issues

- Những hạn chế hoặc lỗi còn tồn tại.

## Installation

Xem hướng dẫn cài đặt tại README.md.
```

Chỉ ghi các thay đổi đã được xác minh trong phiên bản tương ứng.

## 8. Trách nhiệm của Maintainer

Maintainer chịu trách nhiệm:

- Xác nhận điều kiện phát hành.
- Phê duyệt số phiên bản.
- Review Pull Request hướng đến `main`.
- Quản lý Git Tags.
- Công bố GitHub Releases.
- Đảm bảo Changelog và Release Notes chính xác.

## 9. Tài liệu liên quan

- [README](README.md)
- [CHANGELOG](CHANGELOG.md)
- [CONTRIBUTING](CONTRIBUTING.md)
- [GitHub Releases](https://github.com/TieuBao-IT89/CookShare/releases)

---

**CookShare – Release Management Guidelines**
