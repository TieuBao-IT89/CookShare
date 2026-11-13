using System.Data.Common;
using System.Security.Claims;
using CookShare.Data;
using CookShare.Models;
using CookShare.Services;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public sealed class CookedPostsController(ApplicationDbContext db, CookedPostService service) : Controller
    {
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Details([FromRoute] int id, CancellationToken cancellationToken = default)
        {
            string? viewerId = CurrentUserId();
            var post = await db.Set<CookedRecipePost>().AsNoTracking()
                .Where(post => post.Id == id && !post.IsHidden && !post.Recipe.IsHidden)
                .Select(post => new CookedPostViewModel
                {
                    Id = post.Id,
                    RecipeId = post.RecipeId,
                    RecipeName = post.Recipe.Name,
                    AuthorId = post.UserId,
                    AuthorName = post.User.DisplayName ?? post.User.UserName ?? "Thành viên CookShare",
                    AvatarUrl = post.User.AvatarUrl,
                    Note = post.Note,
                    CreatedAt = post.CreatedAt,
                    ImageUrl = CookedPostViewModel.MediaUrl(post.Id, post.ImageFileName),
                    IsOwner = post.UserId == viewerId
                }).SingleOrDefaultAsync(cancellationToken);
            return post == null ? NotFound() : View(post);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? recipeId = null, string? query = null, CancellationToken cancellationToken = default)
        {
            var model = new CookedPostFormViewModel { RecipeId = recipeId ?? 0, Query = query?.Trim() ?? string.Empty };
            if (!await PopulateRecipeAsync(model, cancellationToken))
            {
                return NotFound();
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("cooked-upload")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
        public async Task<IActionResult> Create(CookedPostFormViewModel model, CancellationToken cancellationToken = default)
        {
            string? userId = CurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }
            if (!await PopulateRecipeAsync(model, cancellationToken))
            {
                return NotFound();
            }
            if (model.ImageFile == null)
            {
                ModelState.AddModelError(nameof(model.ImageFile), "Vui lòng chọn một ảnh thành phẩm.");
            }
            if (model.SubmissionKey == Guid.Empty)
            {
                ModelState.AddModelError(nameof(model.SubmissionKey), "Phiên đăng bài không hợp lệ. Vui lòng tải lại trang.");
            }
            if (ModelState.IsValid)
            {
                try
                {
                    int id = await service.CreateAsync(userId, model.RecipeId, model.Note, model.SubmissionKey, model.ImageFile!, cancellationToken);
                    TempData["Success"] = "Đã chia sẻ thành phẩm của bạn.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (RecipeImageUploadException exception)
                {
                    ModelState.AddModelError(nameof(model.ImageFile), exception.Message);
                }
                catch (KeyNotFoundException)
                {
                    return NotFound();
                }
                catch (Exception exception) when (exception is DbUpdateException or DbException or IOException)
                {
                    ModelState.AddModelError(string.Empty, "Chưa thể đăng thành phẩm. Vui lòng chọn lại ảnh và thử lại.");
                }
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit([FromRoute] int id, CancellationToken cancellationToken = default)
        {
            var post = await db.Set<CookedRecipePost>().AsNoTracking().Include(post => post.Recipe)
                .SingleOrDefaultAsync(post => post.Id == id && !post.IsHidden && !post.Recipe.IsHidden, cancellationToken);
            if (post == null)
            {
                return NotFound();
            }
            if (post.UserId != CurrentUserId())
            {
                return Forbid();
            }
            return View(new CookedPostFormViewModel
            {
                PostId = id,
                RecipeId = post.RecipeId,
                RecipeName = post.Recipe.Name,
                Note = post.Note,
                SubmissionKey = post.SubmissionKey,
                ExistingImageUrl = CookedPostViewModel.MediaUrl(post.Id, post.ImageFileName)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("cooked-upload")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
        public async Task<IActionResult> Edit([FromRoute] int id, CookedPostFormViewModel model, CancellationToken cancellationToken = default)
        {
            string? userId = CurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }
            var post = await db.Set<CookedRecipePost>().AsNoTracking().Include(post => post.Recipe)
                .SingleOrDefaultAsync(post => post.Id == id && !post.IsHidden && !post.Recipe.IsHidden, cancellationToken);
            if (post == null)
            {
                return NotFound();
            }
            if (post.UserId != userId)
            {
                return Forbid();
            }
            // Parent identity and existing media always come from the persisted post.
            model.PostId = id;
            model.RecipeId = post.RecipeId;
            model.RecipeName = post.Recipe.Name;
            model.ExistingImageUrl = CookedPostViewModel.MediaUrl(id, post.ImageFileName);
            ModelState.Remove(nameof(model.RecipeId));
            if (ModelState.IsValid)
            {
                try
                {
                    await service.UpdateAsync(id, userId, model.Note, model.ImageFile, cancellationToken);
                    TempData["Success"] = "Đã cập nhật thành phẩm.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (RecipeImageUploadException exception)
                {
                    ModelState.AddModelError(nameof(model.ImageFile), exception.Message);
                }
                catch (KeyNotFoundException)
                {
                    return NotFound();
                }
                catch (UnauthorizedAccessException)
                {
                    return Forbid();
                }
                catch (Exception exception) when (exception is DbUpdateException or DbException or IOException)
                {
                    ModelState.AddModelError(string.Empty, "Chưa thể cập nhật. Vui lòng chọn lại ảnh mới nếu cần và thử lại.");
                }
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken = default)
        {
            string? userId = CurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }
            try
            {
                await service.DeleteAsync(id, userId, cancellationToken);
                TempData["Success"] = "Đã xóa bài thành phẩm.";
                return RedirectToAction("Index", "Profile", new { userId });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception exception) when (exception is DbUpdateException or DbException)
            {
                TempData["Error"] = "Chưa thể xóa bài. Vui lòng thử lại.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        private string? CurrentUserId()
            => User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

        private async Task<bool> PopulateRecipeAsync(CookedPostFormViewModel model, CancellationToken cancellationToken)
        {
            if (model.RecipeId > 0)
            {
                string? name = await db.Recipes.AsNoTracking().Where(recipe => recipe.Id == model.RecipeId && !recipe.IsHidden)
                    .Select(recipe => recipe.Name).SingleOrDefaultAsync(cancellationToken);
                model.RecipeName = name ?? string.Empty;
                return name != null;
            }
            var recipes = db.Recipes.AsNoTracking().Where(recipe => !recipe.IsHidden);
            string query = model.Query.Trim();
            if (query.Length > 100)
            {
                query = query[..100];
            }
            if (query.Length != 0)
            {
                recipes = SocialDiscoveryQueries.Filter(recipes, new DiscoveryOptions(query, "", "", "", null, null, "latest"));
            }
            model.Recipes = await recipes.OrderByDescending(recipe => recipe.CreatedAt).ThenByDescending(recipe => recipe.Id)
                .Take(20).Select(recipe => new CookedPostRecipeChoice(recipe.Id, recipe.Name,
                    recipe.User == null ? null : recipe.User.DisplayName ?? recipe.User.UserName)).ToListAsync(cancellationToken);
            return true;
        }
    }
}
