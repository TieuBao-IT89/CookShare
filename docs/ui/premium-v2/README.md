# Premium V2 — ảnh review

Ảnh Chrome headless, 17/11/2026, từ PostgreSQL fixture Social V2 trên loopback.
Tên người, công thức và ảnh trong fixture chỉ phục vụ kiểm thử; không được seed
vào database ứng dụng. Ảnh trước khi sửa cùng fixture nằm trong
`artifacts/social-v2/premium-before/`; ảnh sau đầy đủ và kết quả tự động nằm trong
`artifacts/premium-v2/` (ignored).

Các lần thử tương tác có thể thay đổi trạng thái thông báo của fixture; ảnh này
dùng để review bố cục và theme, không phải bộ so sánh pixel có state hoàn toàn cố định.

- [Bảng tin desktop, dark](home-dark-1366.png)
- [Bảng tin mobile, dark](home-dark-390.png)
- [Khám phá desktop, dark](discovery-dark-1366.png)
- [Chi tiết desktop, light](details-light-1366.png)
- [Profile mobile, dark](profile-dark-390.png)
- [Editor mobile, dark](create-dark-390.png)

Kích thước viewport: 1366×900 và 390×900. Đây là ảnh viewport, không phải toàn trang.
Ca ảnh thiếu vẫn giữ fallback trung tính, không lấy ảnh ngẫu nhiên để thay thế.
Xem [kết quả và giới hạn kiểm chứng](../../PREMIUM_V2.md).
