# Tài nguyên giao diện bên thứ ba

Premium V2 dùng Lucide SVG, Be Vietnam Pro và Playfair Display local để giao diện không phụ thuộc CDN. Font Awesome là asset lịch sử của Social 2.0, không còn được tải trong layout. Class `fa-*` chỉ giữ làm hook trạng thái tương thích với các module like/save/rating. Không đưa ảnh hoặc giao diện của các website tham khảo vào sản phẩm.

| Tài nguyên | Nguồn và giấy phép | File được giữ |
| --- | --- | --- |
| Lucide / Feather | [Lucide](https://github.com/lucide-icons/lucide), ISC và MIT cho các icon kế thừa Feather | Subset 56 symbol trong `wwwroot/img/icons.svg` (12.009 byte), [LICENSE](../CookShare/wwwroot/lib/lucide/LICENSE.txt); commit và SHA-256 từng icon trong [icons.lock.json](../tools/assets/icons.lock.json). Tái tạo bằng `python tools/assets/Build-IconSprite.py`; không cần chạy để build ứng dụng. |
| Font Awesome Free 7.3.1 | [Gói chính thức](https://www.npmjs.com/package/@fortawesome/fontawesome-free/v/7.3.1), [nguồn](https://github.com/FortAwesome/Font-Awesome); font SIL OFL 1.1, CSS MIT, icon CC BY 4.0 theo thông báo của nhà phát hành | CSS core, solid, regular và hai WOFF2 tương ứng; [LICENSE.txt](../CookShare/wwwroot/lib/fontawesome/LICENSE.txt) |
| Be Vietnam Pro | [Google Fonts](https://github.com/google/fonts/tree/main/ofl/bevietnampro), SIL OFL 1.1 | Latin/tiếng Việt, weight 400/500/600/700/800; [OFL](../CookShare/wwwroot/lib/fonts/be-vietnam-pro-OFL.txt) |
| Playfair Display | [Google Fonts](https://github.com/google/fonts/tree/main/ofl/playfairdisplay), SIL OFL 1.1 | Latin/tiếng Việt; font dùng chung các weight tiêu đề từ CSS upstream; [OFL](../CookShare/wwwroot/lib/fonts/playfair-display-OFL.txt) |

File Font Awesome upstream `all.min.css` được đối chiếu với SRI đã ghim ở baseline; archive npm được kiểm tra SHA-512 theo `dist.integrity`. Chỉ các file cần dùng được lấy từ archive đó. Font Google giữ nguyên byte từ URL nhà phát hành và giữ các giấy phép OFL. SHA-256, kích thước và URL nguồn được lưu trong [fonts.lock.json](../tools/assets/fonts.lock.json).

Ảnh fixture sử dụng các ảnh món đã có trong repository, chỉ gắn vào database kiểm thử riêng. Các trang production không tự sinh bài đăng, đánh giá, người dùng hoặc bộ sưu tập demo.

Khi cập nhật tài nguyên, kiểm tra giấy phép, lấy lại manifest và kiểm tra ảnh hưởng tới bố cục/ký tự tiếng Việt. Không xóa notice/license khỏi thư mục thư viện còn sử dụng.
