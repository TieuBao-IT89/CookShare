## Vấn đề và kết quả

<!-- Nêu vấn đề cụ thể, hành vi trước/sau và lý do thay đổi. -->

## Issue liên quan

<!-- Refs #...; chỉ dùng Closes #... khi giải quyết toàn bộ Issue. -->

## Kiểm tra

<!-- Ghi lệnh, kết quả, test bỏ qua và giới hạn. UI: thêm ảnh trước/sau, desktop/mobile. -->

## Migration và cấu hình

<!-- Nêu migration, biến cấu hình mới, ảnh hưởng dữ liệu/nâng cấp; hoặc ghi Không áp dụng. -->

## Checklist

- [ ] Nhánh tạo từ `develop`, tên theo `CONTRIBUTING.md`, base PR là `develop` (trừ PR phát hành do maintainer quản lý).
- [ ] Diff chỉ chứa thay đổi liên quan, không có credential hoặc dữ liệu cá nhân.
- [ ] Đã chạy kiểm tra phù hợp và ghi rõ phần chưa chạy.
- [ ] Đã cập nhật tài liệu/changelog nếu cần.
- [ ] Nếu sửa schema: migration, snapshot và mô hình đích đồng bộ; đã kiểm tra tích hợp với `develop` mới nhất.
