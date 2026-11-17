using System.Data.Common;
using System.Security.Claims;
using CookShare.Data;
using CookShare.Models;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CookShare.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public sealed class ContentReportsController(ApplicationDbContext db) : Controller
    {
        [HttpGet]
        public async Task<IActionResult> Create(ContentTargetType targetType, int targetId, CancellationToken cancellationToken = default)
        {
            var model = new ContentReportFormViewModel { TargetType = targetType, TargetId = targetId };
            return !Enum.IsDefined(targetType) || !await PopulateTargetAsync(model, cancellationToken) ? NotFound() : View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("reports")]
        public async Task<IActionResult> Create(ContentReportFormViewModel model, CancellationToken cancellationToken = default)
        {
            string? userId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
            if (userId == null)
            {
                return Challenge();
            }
            if (!Enum.IsDefined(model.TargetType) || !await PopulateTargetAsync(model, cancellationToken))
            {
                return NotFound();
            }
            if (model.Reason == null || !Enum.IsDefined(model.Reason.Value))
            {
                ModelState.AddModelError(nameof(model.Reason), "Vui lòng chọn lý do báo cáo hợp lệ.");
            }
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            try
            {
                bool exists = await db.Set<ContentReport>().AnyAsync(report => report.ReporterId == userId
                    && report.TargetType == model.TargetType && report.TargetId == model.TargetId, cancellationToken);
                if (!exists)
                {
                    var report = new ContentReport
                    {
                        ReporterId = userId,
                        TargetType = model.TargetType,
                        TargetId = model.TargetId,
                        Reason = model.Reason!.Value,
                        Details = model.Details?.Trim() ?? string.Empty
                    };
                    db.Set<ContentReport>().Add(report);
                    try
                    {
                        await db.SaveChangesAsync(cancellationToken);
                    }
                    catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation,
                        ConstraintName: "IX_ContentReports_ReporterId_TargetType_TargetId"
                    })
                    {
                        db.Entry(report).State = EntityState.Detached;
                        if (!await db.Set<ContentReport>().AnyAsync(item => item.ReporterId == userId
                            && item.TargetType == model.TargetType && item.TargetId == model.TargetId, cancellationToken))
                        {
                            throw;
                        }
                    }
                }
                TempData["Success"] = "Đã nhận báo cáo. Nhóm kiểm duyệt sẽ xem xét nội dung.";
                return RedirectToAction("Details", model.TargetType == ContentTargetType.Recipe ? "Recipe" : "CookedPosts", new { id = model.TargetId });
            }
            catch (Exception exception) when (exception is DbUpdateException or DbException)
            {
                ModelState.AddModelError(string.Empty, "Chưa thể gửi báo cáo. Vui lòng thử lại.");
                return View(model);
            }
        }

        private async Task<bool> PopulateTargetAsync(ContentReportFormViewModel model, CancellationToken cancellationToken)
        {
            string? name = model.TargetType == ContentTargetType.Recipe
                ? await db.Recipes.AsNoTracking().Where(recipe => recipe.Id == model.TargetId && !recipe.IsHidden)
                    .Select(recipe => recipe.Name).SingleOrDefaultAsync(cancellationToken)
                : await db.Set<CookedRecipePost>().AsNoTracking()
                    .Where(post => post.Id == model.TargetId && !post.IsHidden && !post.Recipe.IsHidden)
                    .Select(post => "Thành phẩm: " + post.Recipe.Name).SingleOrDefaultAsync(cancellationToken);
            model.TargetName = name ?? string.Empty;
            return name != null;
        }
    }
}
