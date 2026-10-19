# Changelog – CookShare

Tất cả những thay đổi đáng chú ý của dự án CookShare sẽ được ghi nhận trong tài liệu này.

Tài liệu được tổ chức dựa trên [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) và định hướng sử dụng [Semantic Versioning](https://semver.org/).

CookShare đang trong quá trình phát triển. Những thay đổi dưới đây chưa được gắn với một GitHub Release chính thức.

## [Unreleased]

### Added

- Bổ sung giấy phép mã nguồn mở MIT License ([PR #35](https://github.com/TieuBao-IT89/CookShare/pull/35)).
- Thêm hướng dẫn đóng góp `CONTRIBUTING.md` ([PR #54](https://github.com/TieuBao-IT89/CookShare/pull/54)).
- Thiết lập GitHub Actions CI cho quá trình Restore và Build .NET 8 ([PR #59](https://github.com/TieuBao-IT89/CookShare/pull/59)).
- Thêm hệ thống Unit Tests bằng xUnit ([PR #60](https://github.com/TieuBao-IT89/CookShare/pull/60)).
- Bổ sung giao diện bình luận tại trang chi tiết công thức ([PR #61](https://github.com/TieuBao-IT89/CookShare/pull/61)).
- Bổ sung chức năng tương tác Like trên giao diện công thức ([PR #62](https://github.com/TieuBao-IT89/CookShare/pull/62)).
- Thêm bộ quy tắc ứng xử `CODE_OF_CONDUCT.md` ([PR #65](https://github.com/TieuBao-IT89/CookShare/pull/65)).

### Changed

- Cập nhật tài liệu `README.md` với hướng dẫn cài đặt, chạy ứng dụng, công nghệ và quy trình đóng góp ([PR #56](https://github.com/TieuBao-IT89/CookShare/pull/56)).
- Bổ sung kiểm tra Coding Standards và Code Quality trong GitHub Actions ([PR #66](https://github.com/TieuBao-IT89/CookShare/pull/66)).

### Fixed

Chưa có thay đổi sửa lỗi cụ thể nào được ghi nhận trong đợt cập nhật Changelog này.

### Security

- Cải thiện `.gitignore` và ngừng theo dõi những file build, file cá nhân không cần thiết ([PR #55](https://github.com/TieuBao-IT89/CookShare/pull/55)).
- Thêm tài liệu chính sách báo cáo và xử lý lỗ hổng bảo mật `SECURITY.md` ([PR #63](https://github.com/TieuBao-IT89/CookShare/pull/63)).

### Deprecated

Chưa có chức năng nào được thông báo ngừng hỗ trợ.

### Removed

Chưa có chức năng nghiệp vụ nào được ghi nhận là bị loại bỏ.

---

## Quy ước cập nhật Changelog

Khi thực hiện thay đổi đáng chú ý, Contributors cần:

1. Xác định thay đổi thuộc nhóm Added, Changed, Deprecated, Removed, Fixed hoặc Security.
2. Bổ sung mô tả ngắn gọn vào mục `Unreleased`.
3. Liên kết Pull Request hoặc Issue liên quan.
4. Không ghi nhận một tính năng chưa được triển khai như đã hoàn thành.
5. Không tự tạo số phiên bản hoặc ngày phát hành.

Khi Maintainer tạo Release chính thức, các thay đổi tương ứng sẽ được chuyển từ `Unreleased` sang mục phiên bản có số phiên bản và ngày phát hành.

Ví dụ cấu trúc phiên bản sau này:

```text
## [Unreleased]

## [1.0.0] - YYYY-MM-DD

### Added
- Những tính năng đã được phát hành.
```

_Ví dụ trên chỉ minh họa cấu trúc, không có nghĩa CookShare đã phát hành v1.0.0._

## Liên kết

- [Repository](https://github.com/TieuBao-IT89/CookShare)
- [GitHub Releases](https://github.com/TieuBao-IT89/CookShare/releases)
- [GitHub Pull Requests](https://github.com/TieuBao-IT89/CookShare/pulls)
- [GitHub Issues](https://github.com/TieuBao-IT89/CookShare/issues)
