# Phát hành CookShare

## Nhánh và phiên bản

Thay đổi thông thường đi qua PR vào `develop`. Maintainer đưa phạm vi đã xác nhận sang `main` qua PR phát hành. Git tag và GitHub Release trỏ đến commit trên `main`.

Dự án dùng [Semantic Versioning](https://semver.org/): `MAJOR.MINOR.PATCH`, tag dạng `vX.Y.Z`. Trong giai đoạn `0.x`, giao diện và hành vi có thể thay đổi. Không di chuyển, ghi đè hoặc xóa tag đã công bố.

Lịch sử phát hành nằm trong [CHANGELOG.md](CHANGELOG.md) và [GitHub Releases](https://github.com/TieuBao-IT89/CookShare/releases). Không tạo số phiên bản/ngày phát hành dự kiến dưới dạng bản đã phát hành.

## Chuẩn bị

1. Chốt phạm vi và kiểm tra các PR liên quan đã được review/merge vào `develop`.
2. Xác nhận không còn lỗi nghiêm trọng đã biết hoặc yêu cầu sửa bắt buộc.
3. Chạy build Release, test PostgreSQL, kiểm tra chất lượng và UI phù hợp theo [Testing](docs/TESTING.md); kiểm tra CI của commit dự kiến phát hành.
4. Review migration, tương thích dữ liệu và khả năng rollback. Sao lưu database và storage ảnh trước khi triển khai có rủi ro.
5. Cập nhật hướng dẫn cài đặt, cấu hình, thay đổi ảnh hưởng người dùng và giới hạn còn lại.

Staging từ `develop` theo [Deployment](docs/DEPLOYMENT.md) phục vụ kiểm chứng, không tự tạo bản phát hành trên `main`.

## Changelog và PR phát hành

Tạo nhánh chuẩn bị từ `develop`, chẳng hạn `chore/prepare-vX.Y.Z`, theo [Contributing](CONTRIBUTING.md). Chuyển các mục tương ứng từ `Unreleased` vào phiên bản đã chọn, dùng ngày phát hành thực tế. Giữ các mục chưa thuộc phạm vi ở `Unreleased`. Gửi PR chuẩn bị vào `develop`.

Sau khi PR chuẩn bị merge và CI đạt, maintainer mở PR `develop` → `main`. Mô tả phạm vi, kết quả kiểm tra, migration, cấu hình mới và vấn đề đã biết. Kiểm tra lại kết quả tích hợp, nhận phê duyệt theo quy định repository rồi merge; không bỏ qua branch protection.

## Tag và GitHub Release

Sau khi `main` chứa đúng commit phát hành đã kiểm tra:

```bash
git fetch origin
git switch main
git merge --ff-only origin/main
git tag -a vX.Y.Z -m "Release vX.Y.Z"
git push origin vX.Y.Z
```

Thay `vX.Y.Z` bằng phiên bản được maintainer xác nhận. Với fork, dùng remote repo gốc có quyền phát hành thay cho `origin`. Nếu không thể fast-forward, kiểm tra khác biệt trước khi tiếp tục.

Tạo bản nháp tại [GitHub Releases](https://github.com/TieuBao-IT89/CookShare/releases/new), chọn tag vừa tạo và viết release notes:

- Điểm nổi bật và thay đổi đáng chú ý.
- Hướng nâng cấp, migration và cấu hình cần bổ sung.
- Giới hạn/vấn đề đã biết và cách giảm ảnh hưởng.
- Liên kết changelog và hướng dẫn cài đặt của đúng tag.

Chỉ publish sau khi maintainer xác nhận nội dung và commit/tag. Không công bố chi tiết lỗ hổng chưa được phối hợp theo [Security Policy](SECURITY.md).

## Sau phát hành

Kiểm tra tag, Release, archive mã nguồn và liên kết tài liệu; thử cài đặt từ tag trong môi trường sạch. Nếu PR phát hành có commit mới trên `main` chưa có ở `develop`, đồng bộ trở lại qua PR theo quy trình repo. Tiếp tục ghi thay đổi mới vào `Unreleased`.

Rollback ứng dụng không tự hạ schema hoặc khôi phục file ảnh. Theo hướng dẫn [Deployment](docs/DEPLOYMENT.md) và [Recipe images](docs/RECIPE_IMAGES.md); không sửa tag để thay thế bản lỗi, phát hành bản vá mới.
