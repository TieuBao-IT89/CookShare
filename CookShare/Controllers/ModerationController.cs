using System.Data;
using System.Data.Common;
using System.Security.Claims;
using CookShare.Data;
using CookShare.Models;
using CookShare.Services;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CookShare.Controllers
{
    [Authorize(Roles = "Moderator")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public sealed class ModerationController(ApplicationDbContext db) : Controller
    {
        [HttpGet]
        public async Task<IActionResult> Index(bool includeResolved = false, int page = 1, CancellationToken cancellationToken = default)
        {
            if (!IsModerator())
            {
                return Forbid();
            }
            if (page is < 1 or > 10000)
            {
                return BadRequest();
            }
            var query = db.Set<ContentReport>().AsNoTracking();
            if (!includeResolved)
            {
                query = query.Where(report => report.Status == ReportStatus.Open);
            }
            var reports = await query.OrderBy(report => report.CreatedAt).ThenBy(report => report.Id)
                .Skip((page - 1) * 25).Take(26).Select(report => new ModerationReportViewModel
                {
                    Id = report.Id,
                    TargetType = report.TargetType,
                    TargetId = report.TargetId,
                    ReporterName = report.Reporter == null ? null : report.Reporter.DisplayName ?? report.Reporter.UserName,
                    Reason = report.Reason,
                    Details = report.Details,
                    Status = report.Status,
                    CreatedAt = report.CreatedAt
                }).ToListAsync(cancellationToken);
            bool hasMore = reports.Count > 25;
            reports = reports.Take(25).ToList();
            await PopulateTargetsAsync(reports, cancellationToken);
            return View(new ModerationQueueViewModel { Reports = reports, IncludeResolved = includeResolved, Page = page, HasMore = hasMore });
        }

        [HttpGet]
        public async Task<IActionResult> Details([FromRoute] int id, CancellationToken cancellationToken = default)
        {
            if (!IsModerator())
            {
                return Forbid();
            }
            var report = await db.Set<ContentReport>().AsNoTracking().Where(report => report.Id == id)
                .Select(report => new ModerationReportViewModel
                {
                    Id = report.Id,
                    TargetType = report.TargetType,
                    TargetId = report.TargetId,
                    ReporterName = report.Reporter == null ? null : report.Reporter.DisplayName ?? report.Reporter.UserName,
                    Reason = report.Reason,
                    Details = report.Details,
                    Status = report.Status,
                    CreatedAt = report.CreatedAt
                }).SingleOrDefaultAsync(cancellationToken);
            if (report == null)
            {
                return NotFound();
            }
            await PopulateTargetsAsync(new[] { report }, cancellationToken);
            if (report.TargetType == ContentTargetType.CookedPost && report.TargetExists)
            {
                report.TargetText = await db.Set<CookedRecipePost>().IgnoreQueryFilters().AsNoTracking()
                    .Where(post => post.Id == report.TargetId).Select(post => post.Note).SingleAsync(cancellationToken);
                report.ReviewImageUrl = $"/Moderation/CookedPostImage/{report.TargetId}";
            }
            else if (report.TargetExists)
            {
                report.TargetText = await db.Recipes.IgnoreQueryFilters().AsNoTracking().Where(recipe => recipe.Id == report.TargetId)
                    .Select(recipe => recipe.Description + "\n\n" + recipe.Ingredients + "\n\n" + recipe.Instructions).SingleAsync(cancellationToken);
                report.ReviewImageUrl = $"/Moderation/RecipeImage/{report.TargetId}";
            }
            report.History = await db.Set<ModerationAction>().AsNoTracking().Where(action => action.ReportId == id)
                .OrderByDescending(action => action.CreatedAt).ThenByDescending(action => action.Id).Take(100)
                .Select(action => new ModerationAuditViewModel(
                    action.Moderator == null ? "Tài khoản đã xóa" : action.Moderator.DisplayName ?? action.Moderator.UserName ?? "Moderator",
                    action.Action, action.Reason, action.CreatedAt)).ToListAsync(cancellationToken);
            return View(report);
        }

        [HttpGet("Moderation/CookedPostImage/{id:int}")]
        public async Task<IActionResult> CookedPostImage([FromRoute] int id, [FromServices] ISocialImageStorage storage,
            CancellationToken cancellationToken = default)
        {
            if (!IsModerator())
            {
                return Forbid();
            }
            string? name = await db.Set<CookedRecipePost>().IgnoreQueryFilters().AsNoTracking()
                .Where(post => post.Id == id).Select(post => post.ImageFileName).SingleOrDefaultAsync(cancellationToken);
            var image = name == null ? null : storage.OpenRead(name);
            return ReviewImage(image);
        }

        [HttpGet("Moderation/RecipeImage/{id:int}")]
        public async Task<IActionResult> RecipeImage([FromRoute] int id, [FromServices] IRecipeImageStorage storage,
            CancellationToken cancellationToken = default)
        {
            if (!IsModerator())
            {
                return Forbid();
            }
            string? reference = await db.Recipes.IgnoreQueryFilters().AsNoTracking()
                .Where(recipe => recipe.Id == id).Select(recipe => recipe.Image).SingleOrDefaultAsync(cancellationToken);
            const string prefix = "/uploads/recipes/";
            var image = reference?.StartsWith(prefix, StringComparison.Ordinal) == true ? storage.OpenRead(reference[prefix.Length..]) : null;
            return ReviewImage(image);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Decide([FromRoute] int id, ModerationDecisionViewModel model, CancellationToken cancellationToken = default)
        {
            string? moderatorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!IsModerator() || string.IsNullOrEmpty(moderatorId))
            {
                return Forbid();
            }
            if (!ModelState.IsValid || !Enum.IsDefined(model.Action) || string.IsNullOrWhiteSpace(model.Reason)
                || model.Reason.Trim().Length is < 3 or > 1000)
            {
                TempData["Error"] = "Vui lòng chọn thao tác hợp lệ và ghi lý do từ 3 đến 1000 ký tự.";
                return RedirectToAction(nameof(Details), new { id });
            }
            try
            {
                // Serialize decisions on one report; status, visibility and audit commit together.
                await using IDbContextTransaction? transaction = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
                var report = await db.Set<ContentReport>().SingleOrDefaultAsync(report => report.Id == id, cancellationToken);
                if (report == null)
                {
                    return NotFound();
                }
                Recipe? recipe = null;
                CookedRecipePost? post = null;
                if (report.TargetType == ContentTargetType.Recipe)
                {
                    recipe = await db.Recipes.IgnoreQueryFilters().SingleOrDefaultAsync(recipe => recipe.Id == report.TargetId, cancellationToken);
                }
                else
                {
                    post = await db.Set<CookedRecipePost>().IgnoreQueryFilters().SingleOrDefaultAsync(post => post.Id == report.TargetId, cancellationToken);
                }
                if (model.Action != ModerationActionType.Dismiss && recipe == null && post == null)
                {
                    TempData["Error"] = "Nội dung đã bị xóa; chỉ có thể bỏ qua báo cáo.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                ReportStatus nextStatus = model.Action switch
                {
                    ModerationActionType.Hide => ReportStatus.Hidden,
                    ModerationActionType.Restore => ReportStatus.Restored,
                    _ => ReportStatus.Dismissed
                };
                bool? hidden = recipe?.IsHidden ?? post?.IsHidden;
                bool visibilityAlreadyMatches = model.Action == ModerationActionType.Dismiss
                    || hidden == (model.Action == ModerationActionType.Hide);
                if (report.Status == nextStatus && visibilityAlreadyMatches)
                {
                    return RedirectToAction(nameof(Details), new { id });
                }
                if (model.Action != ModerationActionType.Dismiss)
                {
                    if (recipe != null)
                    {
                        recipe.IsHidden = model.Action == ModerationActionType.Hide;
                    }
                    if (post != null)
                    {
                        post.IsHidden = model.Action == ModerationActionType.Hide;
                    }
                }
                report.Status = nextStatus;
                report.ResolvedAt = DateTime.UtcNow;
                db.Set<ModerationAction>().Add(new ModerationAction
                {
                    ReportId = report.Id,
                    ModeratorId = moderatorId,
                    TargetType = report.TargetType,
                    TargetId = report.TargetId,
                    Action = model.Action,
                    Reason = model.Reason.Trim()
                });
                await db.SaveChangesAsync(cancellationToken);
                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
                TempData["Success"] = "Đã xử lý báo cáo và lưu nhật ký.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception exception) when (exception is DbUpdateException or DbException)
            {
                TempData["Error"] = "Chưa thể xử lý báo cáo. Vui lòng tải lại và thử lại.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        private bool IsModerator() => User.Identity?.IsAuthenticated == true && User.IsInRole("Moderator");

        private IActionResult ReviewImage(RecipeImageFile? image)
        {
            if (image == null)
            {
                return NotFound();
            }
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers.CacheControl = "no-store";
            return File(image.Content, image.ContentType, enableRangeProcessing: false);
        }

        private async Task PopulateTargetsAsync(IReadOnlyList<ModerationReportViewModel> reports, CancellationToken cancellationToken)
        {
            int[] recipeIds = reports.Where(report => report.TargetType == ContentTargetType.Recipe).Select(report => report.TargetId).ToArray();
            int[] postIds = reports.Where(report => report.TargetType == ContentTargetType.CookedPost).Select(report => report.TargetId).ToArray();
            var recipes = await db.Recipes.IgnoreQueryFilters().AsNoTracking().Where(recipe => recipeIds.Contains(recipe.Id))
                .Select(recipe => new { recipe.Id, recipe.Name, recipe.IsHidden }).ToDictionaryAsync(recipe => recipe.Id, cancellationToken);
            var posts = await db.Set<CookedRecipePost>().IgnoreQueryFilters().AsNoTracking().Where(post => postIds.Contains(post.Id))
                .Select(post => new { post.Id, Name = "Thành phẩm: " + post.Recipe.Name, IsHidden = post.IsHidden || post.Recipe.IsHidden })
                .ToDictionaryAsync(post => post.Id, cancellationToken);
            foreach (var report in reports)
            {
                var target = report.TargetType == ContentTargetType.Recipe
                    ? recipes.GetValueOrDefault(report.TargetId) : posts.GetValueOrDefault(report.TargetId);
                report.TargetExists = target != null;
                report.TargetName = target?.Name ?? "Nội dung đã bị xóa";
                report.TargetIsHidden = target?.IsHidden ?? false;
            }
        }
    }
}
