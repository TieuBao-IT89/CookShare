using System.ComponentModel.DataAnnotations;
using CookShare.Models.Identity;

namespace CookShare.Models
{
    public enum ContentTargetType { Recipe, CookedPost }
    public enum ReportReason { Spam, Harassment, UnsafeContent, Copyright, Other }
    public enum ReportStatus { Open, Dismissed, Hidden, Restored }
    public enum ModerationActionType { Dismiss, Hide, Restore }

    public sealed class ContentReport
    {
        public int Id { get; set; }
        public string? ReporterId { get; set; }
        // Durable audit identity: intentionally no FK to deletable user content.
        public ContentTargetType TargetType { get; set; }
        public int TargetId { get; set; }
        public ReportReason Reason { get; set; }
        [MaxLength(1000)]
        public string Details { get; set; } = string.Empty;
        public ReportStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
        public ApplicationUser? Reporter { get; set; }
    }

    public sealed class ModerationAction
    {
        public int Id { get; set; }
        public int ReportId { get; set; }
        public string? ModeratorId { get; set; }
        public ContentTargetType TargetType { get; set; }
        public int TargetId { get; set; }
        public ModerationActionType Action { get; set; }
        [Required, MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ContentReport Report { get; set; } = null!;
        public ApplicationUser? Moderator { get; set; }
    }
}
