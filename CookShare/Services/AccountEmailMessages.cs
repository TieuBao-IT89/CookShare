using System.Text.Encodings.Web;

namespace CookShare.Services
{
    public static class AccountEmailMessages
    {
        public static (string Subject, string Html) ConfirmEmail(
            string confirmUrl)
        {
            return (
                "Xác minh email CookShare",
                Body(
                    "Xác minh email",
                    "Liên kết này hết hạn sau 1 giờ.",
                    confirmUrl));
        }

        public static (string Subject, string Html) ChangeEmail(
            string confirmUrl)
        {
            return (
                "Xác nhận email mới trên CookShare",
                Body(
                    "Xác nhận email mới",
                    "Email chỉ đổi sau khi bạn mở liên kết này. Liên kết hết hạn sau 1 giờ.",
                    confirmUrl));
        }

        public static (string Subject, string Html) PasswordReset(
            string resetUrl)
        {
            return (
                "Đặt lại mật khẩu CookShare",
                Body(
                    "Đặt lại mật khẩu",
                    "Liên kết này hết hạn sau 1 giờ. Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.",
                    resetUrl));
        }

        private static string Body(
            string action,
            string note,
            string url)
        {
            var encodedUrl = HtmlEncoder.Default.Encode(url);
            var encodedAction = HtmlEncoder.Default.Encode(action);
            var encodedNote = HtmlEncoder.Default.Encode(note);

            return
                "<p>Xin chào,</p>" +
                "<p><a href=\"" + encodedUrl + "\">" +
                encodedAction +
                "</a></p><p>" +
                encodedNote +
                "</p>";
        }
    }
}
