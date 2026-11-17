using System.Data.Common;
using System.Security.Claims;
using CookShare.Data;
using CookShare.Services;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Controllers
{
    [Authorize]
    [Route("Profile/Highlights")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public sealed class ProfileHighlightsController(ApplicationDbContext db, ILogger<ProfileHighlightsController> logger) : Controller
    {
        private const string ViewPath = "~/Views/Profile/Highlights.cshtml";
        private static readonly Action<ILogger, string, Exception?> _logFailure = LoggerMessage.Define<string>(
            LogLevel.Warning, new EventId(1118, "ProfileHighlightsFailure"), "Profile highlights request failed with {FailureType}.");

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            string? viewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(viewerId)) return Challenge();
            var model = new ProfileHighlightsViewModel();
            try
            {
                if (!await db.Users.AnyAsync(user => user.Id == viewerId, cancellationToken)) return Challenge();
                model.RecipeIds = await db.ProfileFeaturedRecipes.AsNoTracking()
                    .Where(pin => pin.UserId == viewerId && pin.Recipe.UserId == viewerId)
                    .OrderBy(pin => pin.DisplayOrder).Select(pin => pin.RecipeId).ToListAsync(cancellationToken);
                await PopulateAsync(model, viewerId, cancellationToken);
            }
            catch (DbException exception)
            {
                Failed(exception);
            }

            return View(ViewPath, model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(ProfileHighlightsViewModel model, CancellationToken cancellationToken)
        {
            string? viewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(viewerId)) return Challenge();
            try
            {
                if (!await db.Users.AnyAsync(user => user.Id == viewerId, cancellationToken)) return Challenge();
                await PopulateAsync(model, viewerId, cancellationToken);
                int[] selected = model.RecipeIds.Where(id => id != 0).ToArray();
                if (model.RecipeIds.Count > 3 || selected.Distinct().Count() != selected.Length)
                {
                    ModelState.AddModelError(nameof(model.RecipeIds), "Chọn tối đa 3 công thức khác nhau.");
                }

                if (!ModelState.IsValid) return View(ViewPath, model);
                if (!await new ProfileHighlightsService(db).ReplaceAsync(viewerId, selected, cancellationToken))
                {
                    ModelState.AddModelError(nameof(model.RecipeIds), "Chỉ chọn công thức do chính bạn đăng và vẫn còn tồn tại.");
                    return View(ViewPath, model);
                }

                TempData["Success"] = "Đã cập nhật những món ngon nổi bật của bạn.";
                return RedirectToAction("Index", "Profile");
            }
            catch (Exception exception) when (exception is DbUpdateException or DbException)
            {
                db.ChangeTracker.Clear();
                Failed(exception);
                return View(ViewPath, model);
            }
        }

        private async Task PopulateAsync(ProfileHighlightsViewModel model, string viewerId, CancellationToken cancellationToken)
        {
            model.AvailableRecipes = await db.Recipes.AsNoTracking().Where(recipe => recipe.UserId == viewerId)
                .OrderByDescending(recipe => recipe.CreatedAt).ThenByDescending(recipe => recipe.Id).ToListAsync(cancellationToken);
            ViewData["RecipeRatingSummaries"] = await RecipeRatingQueries.LoadAsync(db, model.AvailableRecipes.Select(recipe => recipe.Id), viewerId);
        }

        private void Failed(Exception exception)
        {
            _logFailure(logger, exception.GetType().Name, null);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ModelState.AddModelError(string.Empty, "Chưa thể lưu công thức nổi bật. Lựa chọn của bạn được giữ lại; vui lòng thử lại sau.");
        }
    }
}
