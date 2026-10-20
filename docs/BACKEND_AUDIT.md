BÁO CÁO KIỂM TRA HIỆN TRẠNG BACKEND COOKSHARE
Issue: #72 — Kiểm tra chức năng đã triển khai và mức độ sẵn sàng của Backend
Vị trí trong repository: docs/BACKEND_AUDIT.md
Trạng thái: Báo cáo rà soát ban đầu; cần xác minh bằng kiểm thử thực tế và maintainer review.

MỤC LỤC

1. Mục tiêu
2. Cấu trúc dự án
3. Bảng kiểm tra chức năng
4. Dữ liệu mẫu và localStorage
5. Xác thực và phân quyền
6. Quản lý công thức CRUD
7. Like, Comment và Profile
8. Kiểm thử và GitHub Actions
9. Đối chiếu Issues đã đóng
10. Công việc đề xuất
11. Checklist hoàn thành
12. Kết luận
13. MỤC TIÊU
    Báo cáo rà soát mã nguồn CookShare nhằm phân biệt chức năng đã có giao diện, chức năng chỉ xử lý ở trình duyệt, chức năng backend đã được xác minh và những thành phần còn thiếu để lưu dữ liệu lâu dài.

Phạm vi: Controllers, Models, Razor Views, JavaScript, xác thực, CRUD công thức, Like, Comment, Profile, database, kiểm thử và CI.
Lưu ý: Đây là kết quả rà soát ban đầu dựa trên mã nguồn đã cung cấp. Nội dung chưa chạy thử hoặc chưa đối chiếu trực tiếp trên GitHub được đánh dấu là cần xác minh.

2. CẤU TRÚC DỰ ÁN
   Dự án sử dụng ASP.NET Core MVC trên nền tảng .NET 8.
   Thành phần Vai trò Kết quả rà soát ban đầu
   Controllers Nhận yêu cầu và điều phối xử lý Có controller cho các trang/chức năng chính; cần xác minh từng action
   Models Mô tả dữ liệu Có model công thức; cần đối chiếu với dữ liệu thực tế
   Razor Views (.cshtml) Hiển thị giao diện Có các trang giao diện cho chức năng chính
   JavaScript Xử lý tương tác trình duyệt Có logic cho công thức và tương tác; một số dữ liệu dùng localStorage
   Program.cs Cấu hình ứng dụng Chưa thấy cấu hình Entity Framework Core, database hoặc ASP.NET Core Identity trong mã nguồn đã rà soát
   Dự án kiểm thử Kiểm tra hành vi Cần chạy test để xác nhận kết quả
   GitHub Actions Tự động build/test Có workflow; cần kiểm tra lần chạy mới nhất

3. BẢNG KIỂM TRA CHỨC NĂNG
   Cách đọc trạng thái: “Có giao diện” nghĩa là tìm thấy thành phần giao diện; “Chưa xác nhận” nghĩa là chưa đủ bằng chứng để kết luận chức năng chạy hoàn chỉnh; “Cần kiểm thử” nghĩa là phải chạy ứng dụng và kiểm tra thực tế.
   Chức năng Trạng thái ban đầu Việc cần xác minh
   Trang chủ và điều hướng Có giao diện Kiểm tra liên kết và điều hướng
   Đăng ký tài khoản Chưa xác nhận backend Tạo tài khoản và kiểm tra nơi lưu dữ liệu
   Đăng nhập Chưa xác nhận backend Thử đăng nhập đúng/sai và kiểm tra xác thực
   Đăng xuất Chưa xác nhận Kiểm tra kết thúc phiên và truy cập lại trang được bảo vệ
   Danh sách công thức Có giao diện/logic trình duyệt Kiểm tra nguồn dữ liệu
   Chi tiết công thức Có giao diện; có sử dụng JavaScript/localStorage Thử ID không tồn tại và tải lại trang
   Tạo công thức Có giao diện/logic trình duyệt Xác minh dữ liệu có được lưu tại máy chủ không
   Sửa công thức Chưa xác nhận CRUD backend Kiểm tra quyền sửa và lưu thay đổi
   Xóa công thức Chưa xác nhận CRUD backend Kiểm tra quyền xóa và xác nhận thao tác
   Like/Bỏ Like Có logic trình duyệt Kiểm tra lưu trữ, đồng bộ và dữ liệu theo tài khoản
   Comment Có mô-đun JavaScript Kiểm tra thêm, hiển thị, xóa và lưu bình luận
   Công thức đã lưu Có logic trình duyệt Kiểm tra lưu/bỏ lưu giữa các lần truy cập
   Profile Có trang/controller Xác minh dữ liệu có gắn với tài khoản thực tế không
   Database Chưa thấy cấu hình trong phần đã rà soát Kiểm tra package, connection string, DbContext, migration
   Automated tests Có dự án kiểm thử theo cấu trúc đã rà soát Chạy toàn bộ test và ghi số lượng đạt/trượt
   CI Có workflow GitHub Actions Kiểm tra build, test, định dạng và lần chạy mới nhất

4. DỮ LIỆU MẪU VÀ localStorage
   localStorage là vùng lưu trữ trong trình duyệt. Dữ liệu lưu tại đây thường gắn với trình duyệt và thiết bị đang sử dụng; không tự động chia sẻ với người dùng khác hoặc thiết bị khác.
   Rủi ro cần lưu ý
5. Dữ liệu có thể mất khi người dùng xóa dữ liệu trình duyệt.
6. Dữ liệu trên nhiều máy hoặc trình duyệt không tự đồng bộ.
7. localStorage không thể thay thế xác thực và phân quyền an toàn ở phía máy chủ.
8. Dữ liệu mẫu viết trực tiếp trong View/JavaScript không phải dữ liệu từ database.
   Việc cần làm để xác minh
   Tìm kiếm trong mã nguồn các từ khóa localStorage, dữ liệu mẫu, mảng khởi tạo sẵn, fetch, URL API và các lời gọi HTTP. Khi hoàn thiện báo cáo, bổ sung đường dẫn tệp và số dòng làm bằng chứng cho từng kết luận quan trọng.
9. XÁC THỰC VÀ PHÂN QUYỀN
   Chưa xác nhận hệ thống xác thực phía máy chủ đã hoàn thiện. Cần kiểm tra:

- Có dùng ASP.NET Core Identity hoặc cơ chế xác thực máy chủ tương đương không.
- Mật khẩu được xử lý bằng cơ chế bảo mật phù hợp không.
- Các trang/thao tác cần đăng nhập có được bảo vệ phía máy chủ không.
- Người dùng chỉ có thể sửa hoặc xóa công thức của chính họ không.
- Đăng xuất có kết thúc phiên xác thực thực sự hay chỉ thay đổi giao diện.
  Không đánh dấu xác thực là hoàn thiện chỉ vì giao diện đăng nhập hoạt động.

6. QUẢN LÝ CÔNG THỨC CRUD
   CRUD gồm Create (tạo), Read (xem), Update (chỉnh sửa) và Delete (xóa).
   Luồng cần xác minh
1. Giao diện gửi dữ liệu đến Controller/API.
1. Máy chủ kiểm tra dữ liệu đầu vào.
1. Máy chủ xác minh quyền người dùng.
1. Dữ liệu được lưu hoặc cập nhật trong database.
1. Máy chủ trả kết quả phù hợp về giao diện.
1. Tải lại trang vẫn thấy dữ liệu đã lưu.
   Nếu thao tác chỉ thay đổi JavaScript hoặc localStorage, hãy ghi rõ đây là xử lý phía trình duyệt, chưa phải CRUD lưu trữ tập trung.
1. LIKE, COMMENT VÀ PROFILE
   7.1. Like

- Xác minh Like gắn với tài khoản và công thức cụ thể.
- Kiểm tra có ngăn một tài khoản Like trùng lặp không.
- Xác minh trạng thái Like được lưu ở máy chủ hay chỉ ở trình duyệt.
  7.2. Comment
- Kiểm tra tạo, hiển thị và xóa bình luận.
- Kiểm tra dữ liệu đầu vào và thông tin người tạo bình luận.
- Xác minh bình luận còn tồn tại sau khi đăng xuất hoặc mở bằng thiết bị khác.
  7.3. Profile
- Xác minh hồ sơ lấy từ tài khoản đang đăng nhập.
- Kiểm tra người dùng không thể xem/sửa dữ liệu riêng tư trái phép.
- Xác minh danh sách công thức phản ánh dữ liệu thực tế.

8. KIỂM THỬ VÀ GITHUB ACTIONS
   Chạy các lệnh sau tại thư mục chứa solution (điều chỉnh tên solution nếu khác):
   dotnet restore CookShare.sln
   dotnet build CookShare.sln --configuration Release
   dotnet test CookShare.sln --configuration Release
   Ghi lại kết quả
   Hạng mục Kết quả cần ghi
   Restore Thành công/thất bại và lỗi nếu có
   Build Thành công/thất bại và cảnh báo quan trọng
   Test Tổng số test, số đạt, số trượt, số bị bỏ qua
   GitHub Actions Liên kết lần chạy và trạng thái
   Vấn đề còn tồn tại Mô tả lỗi và bước tái hiện

Có workflow hoặc test project không tự chứng minh pipeline đã chạy thành công. Cần kiểm tra tab Actions trên GitHub. 9. ĐỐI CHIẾU ISSUES ĐÃ ĐÓNG
Trước khi tạo Issue mới:

1. Mở danh sách Issues đã đóng của repository.
2. Tìm các Issue về xác thực, CRUD công thức, Like, Comment, Profile, database và kiểm thử.
3. Đọc mô tả Issue và Pull Request liên quan.
4. Đối chiếu thay đổi đã merge với mã nguồn hiện tại.
5. Ghi rõ chức năng đã triển khai, triển khai một phần hay chưa có bằng chứng xác minh.
   Không kết luận chức năng hoàn thiện chỉ vì Issue đã đóng hoặc Pull Request đã merge.
6. CÔNG VIỆC ĐỀ XUẤT THEO MỨC ƯU TIÊN
   P0 — Xác minh nền tảng

- Chạy build và test; ghi lại kết quả.
- Xác định các chức năng đang dùng dữ liệu mẫu hoặc localStorage.
- Xác nhận nhánh tích hợp và trạng thái CI của repository.
  P1 — Backend và dữ liệu
- Thống nhất mô hình dữ liệu và hệ quản trị cơ sở dữ liệu.
- Cấu hình Entity Framework Core, DbContext, chuỗi kết nối và migration.
- Triển khai CRUD công thức phía máy chủ, kiểm tra dữ liệu và quyền truy cập.
  P2 — Tài khoản và tương tác
- Triển khai/hoàn thiện xác thực và phân quyền phía máy chủ.
- Lưu Like, Comment và công thức đã lưu trong database.
- Kết nối Profile với tài khoản và công thức thực tế.
  P3 — Chất lượng và vận hành
- Bổ sung unit test và integration test cho luồng quan trọng.
- Tích hợp báo cáo độ phủ mã nếu repository yêu cầu.
- Rà soát Dependabot, cảnh báo bảo mật và chất lượng workflow CI.
- Cập nhật tài liệu thiết lập môi trường cho thành viên nhóm.
  Đây là thứ tự đề xuất ban đầu. Cần đối chiếu Issues hiện có để tránh tạo công việc trùng lặp.

11. CHECKLIST HOÀN THÀNH AUDIT

- Đã rà soát Controllers, Models, Views và JavaScript.
- Đã ghi đường dẫn tệp và số dòng làm bằng chứng cho kết luận chính.
- Đã chạy thử đăng ký, đăng nhập và đăng xuất.
- Đã kiểm tra tạo, xem, sửa và xóa công thức.
- Đã kiểm tra Like, Comment và Profile.
- Đã xác định dữ liệu hardcode và dữ liệu dùng localStorage.
- Đã xác minh cấu hình và khả năng lưu dữ liệu database.
- Đã chạy build/test và ghi lại kết quả thực tế.
- Đã đối chiếu Issues đã đóng và Pull Request liên quan.
- Đã tạo Pull Request và yêu cầu maintainer review.

12. KẾT LUẬN
    Mã nguồn có các thành phần giao diện và logic phía trình duyệt cho nhiều chức năng CookShare. Tuy nhiên, cần kiểm chứng riêng từng luồng để phân biệt giao diện demo với chức năng backend có xác thực, phân quyền và lưu trữ dữ liệu lâu dài.

Đây là báo cáo rà soát ban đầu. Chỉ chuyển trạng thái thành “đã xác minh” khi có bằng chứng từ mã nguồn cụ thể hoặc kết quả chạy thực tế. Chỉ đóng Issue sau khi các tiêu chí chấp nhận được đáp ứng và maintainer đã review, xác nhận.
