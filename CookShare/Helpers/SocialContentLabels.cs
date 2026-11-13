using CookShare.Models;

namespace CookShare.Helpers
{
    public static class SocialContentLabels
    {
        public static string Reason(ReportReason reason) => reason switch
        {
            ReportReason.Spam => "Spam hoặc quảng cáo",
            ReportReason.Harassment => "Quấy rối hoặc công kích",
            ReportReason.UnsafeContent => "Nội dung nguy hiểm hoặc không phù hợp",
            ReportReason.Copyright => "Quyền sử dụng nội dung",
            _ => "Lý do khác"
        };

        public static string Status(ReportStatus status) => status switch
        {
            ReportStatus.Open => "Cần xử lý",
            ReportStatus.Dismissed => "Đã bỏ qua",
            ReportStatus.Hidden => "Đã ẩn",
            ReportStatus.Restored => "Đã khôi phục",
            _ => "Không xác định"
        };

        public static string Action(ModerationActionType action) => action switch
        {
            ModerationActionType.Hide => "Ẩn nội dung",
            ModerationActionType.Restore => "Khôi phục nội dung",
            _ => "Bỏ qua báo cáo"
        };
    }
}
