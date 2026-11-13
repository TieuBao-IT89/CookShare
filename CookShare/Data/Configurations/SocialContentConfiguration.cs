using CookShare.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookShare.Data.Configurations
{
    public sealed class CookedRecipePostConfiguration : IEntityTypeConfiguration<CookedRecipePost>
    {
        public void Configure(EntityTypeBuilder<CookedRecipePost> builder)
        {
            builder.Property(post => post.UserId).HasMaxLength(450);
            builder.HasIndex(post => new { post.UserId, post.SubmissionKey }).IsUnique();
            builder.HasIndex(post => new { post.CreatedAt, post.Id });
            builder.HasIndex(post => new { post.RecipeId, post.CreatedAt, post.Id });
            builder.HasIndex(post => new { post.UserId, post.CreatedAt, post.Id });
            builder.HasOne(post => post.User).WithMany().HasForeignKey(post => post.UserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(post => post.Recipe).WithMany().HasForeignKey(post => post.RecipeId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
    {
        public void Configure(EntityTypeBuilder<ContentReport> builder)
        {
            builder.Property(report => report.TargetType).HasConversion<string>().HasMaxLength(20);
            builder.Property(report => report.Reason).HasConversion<string>().HasMaxLength(30);
            builder.Property(report => report.Status).HasConversion<string>().HasMaxLength(20);
            builder.HasIndex(report => new { report.ReporterId, report.TargetType, report.TargetId }).IsUnique();
            builder.HasIndex(report => new { report.Status, report.CreatedAt, report.Id });
            builder.HasOne(report => report.Reporter).WithMany().HasForeignKey(report => report.ReporterId).OnDelete(DeleteBehavior.SetNull);
            builder.ToTable(table => table.HasCheckConstraint("CK_ContentReports_TargetId", "\"TargetId\" > 0"));
        }
    }

    public sealed class ModerationActionConfiguration : IEntityTypeConfiguration<ModerationAction>
    {
        public void Configure(EntityTypeBuilder<ModerationAction> builder)
        {
            builder.Property(action => action.TargetType).HasConversion<string>().HasMaxLength(20);
            builder.Property(action => action.Action).HasConversion<string>().HasMaxLength(20);
            builder.HasIndex(action => new { action.ReportId, action.CreatedAt });
            builder.HasOne(action => action.Report).WithMany().HasForeignKey(action => action.ReportId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(action => action.Moderator).WithMany().HasForeignKey(action => action.ModeratorId).OnDelete(DeleteBehavior.SetNull);
        }
    }
}
