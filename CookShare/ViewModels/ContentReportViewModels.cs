using System.ComponentModel.DataAnnotations;
using CookShare.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CookShare.ViewModels
{
    public sealed class ContentReportFormViewModel
    {
        [EnumDataType(typeof(ContentTargetType))]
        public ContentTargetType TargetType { get; set; }
        [Range(1, int.MaxValue)]
        public int TargetId { get; set; }
        [Required(ErrorMessage = "Vui lòng chọn lý do báo cáo.")]
        [EnumDataType(typeof(ReportReason))]
        public ReportReason? Reason { get; set; }
        [StringLength(1000, ErrorMessage = "Chi tiết không được vượt quá 1000 ký tự.")]
        public string Details { get; set; } = string.Empty;
        [BindNever]
        public string TargetName { get; set; } = string.Empty;
    }

    public sealed class ModerationDecisionViewModel
    {
        [EnumDataType(typeof(ModerationActionType))]
        public ModerationActionType Action { get; set; }
        [Required(ErrorMessage = "Vui lòng ghi lý do xử lý.")]
        [StringLength(1000, MinimumLength = 3, ErrorMessage = "Lý do cần từ 3 đến 1000 ký tự.")]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class ModerationQueueViewModel
    {
        public IReadOnlyList<ModerationReportViewModel> Reports { get; set; } = Array.Empty<ModerationReportViewModel>();
        public bool IncludeResolved { get; set; }
        public int Page { get; set; }
        public bool HasMore { get; set; }
    }

    public sealed class ModerationReportViewModel
    {
        public int Id { get; set; }
        public ContentTargetType TargetType { get; set; }
        public int TargetId { get; set; }
        public string TargetName { get; set; } = string.Empty;
        public string? ReporterName { get; set; }
        public ReportReason Reason { get; set; }
        public string Details { get; set; } = string.Empty;
        public ReportStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool TargetExists { get; set; }
        public bool TargetIsHidden { get; set; }
        public string TargetText { get; set; } = string.Empty;
        public string? ReviewImageUrl { get; set; }
        public IReadOnlyList<ModerationAuditViewModel> History { get; set; } = Array.Empty<ModerationAuditViewModel>();
    }

    public sealed record ModerationAuditViewModel(string ModeratorName, ModerationActionType Action, string Reason, DateTime CreatedAt);
}
