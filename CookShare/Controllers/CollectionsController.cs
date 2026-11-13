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
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public sealed class CollectionsController(RecipeCollectionService collections, ApplicationDbContext db) : Controller
    {
        [HttpGet("Collections")]
        public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
        {
            string? userId = CurrentUserId();
            if (userId == null) return Challenge();
            try
            {
                return View(await collections.ListOwnedAsync(userId, page, cancellationToken));
            }
            catch (Exception exception) when (IsDatabaseFailure(exception))
            {
                return Unavailable();
            }
        }

        [AllowAnonymous]
        [HttpGet("Collections/{id:int}")]
        public async Task<IActionResult> Details([FromRoute] int id, int page = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                var model = await collections.ReadAsync(id, CurrentUserId(), page, cancellationToken);
                if (model == null) return NotFound();
                ViewData["SavedRecipeIds"] = CurrentUserId() is string userId
                    ? (await db.Favorites.AsNoTracking().Where(favorite => favorite.UserId == userId
                        && model.Recipes.Select(recipe => recipe.Id).Contains(favorite.RecipeId))
                        .Select(favorite => favorite.RecipeId).ToListAsync(cancellationToken)).ToHashSet()
                    : new HashSet<int>();
                return View(model);
            }
            catch (Exception exception) when (IsDatabaseFailure(exception))
            {
                return Unavailable();
            }
        }

        [HttpGet("Recipe/{recipeId:int}/Collections")]
        public async Task<IActionResult> Chooser([FromRoute] int recipeId, string? returnUrl = null, int page = 1, CancellationToken cancellationToken = default)
        {
            string? userId = CurrentUserId();
            if (userId == null) return Challenge();
            try
            {
                var model = await collections.ChooserAsync(userId, recipeId, page, SafeReturnUrl(returnUrl), cancellationToken);
                if (model == null) return NotFound();
                return IsEnhancedRequest() ? PartialView("_Chooser", model) : View(model);
            }
            catch (Exception exception) when (IsDatabaseFailure(exception))
            {
                return Unavailable();
            }
        }

        [HttpGet("Collections/Create")]
        public IActionResult Create()
        {
            return CurrentUserId() == null ? Challenge() : View(new CollectionInputViewModel());
        }

        [HttpPost("Collections/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CollectionInputViewModel input, CancellationToken cancellationToken = default)
        {
            string? userId = CurrentUserId();
            if (userId == null) return Challenge();
            try
            {
                if (!ModelState.IsValid || string.IsNullOrWhiteSpace(input.Name))
                {
                    if (string.IsNullOrWhiteSpace(input.Name) && !ModelState.ContainsKey(nameof(input.Name)))
                        ModelState.AddModelError(nameof(input.Name), "Hãy đặt tên cho bộ sưu tập.");
                    if (!input.RecipeId.HasValue) return View(input);
                    var chooser = await collections.ChooserAsync(userId, input.RecipeId.Value, 1, SafeReturnUrl(input.ReturnUrl), cancellationToken);
                    if (chooser == null) return NotFound();
                    chooser.NewCollection = input;
                    Response.StatusCode = StatusCodes.Status400BadRequest;
                    return IsEnhancedRequest() ? PartialView("_Chooser", chooser) : View("Chooser", chooser);
                }
                var created = await collections.CreateAsync(userId, input, cancellationToken);
                if (created == null) return NotFound();
                string next = input.RecipeId.HasValue
                    ? Url.Action(nameof(Chooser), new { recipeId = input.RecipeId.Value, returnUrl = SafeReturnUrl(input.ReturnUrl) })!
                    : Url.Action(nameof(Details), new { id = created.Id })!;
                return IsEnhancedRequest()
                    ? Json(new { collectionId = created.Id, message = "Đã tạo bộ sưu tập riêng tư và thêm công thức.", next })
                    : LocalRedirect(next);
            }
            catch (Exception exception) when (IsDatabaseFailure(exception))
            {
                return Unavailable();
            }
        }

        [HttpGet("Collections/{id:int}/Edit")]
        public async Task<IActionResult> Edit([FromRoute] int id, CancellationToken cancellationToken = default)
        {
            string? userId = CurrentUserId();
            if (userId == null) return Challenge();
            try
            {
                var collection = await collections.OwnedAsync(userId, id, cancellationToken);
                if (collection == null) return NotFound();
                ViewData["CollectionId"] = id;
                return View(new CollectionInputViewModel { Name = collection.Name, Description = collection.Description, IsPublic = collection.IsPublic });
            }
            catch (Exception exception) when (IsDatabaseFailure(exception))
            {
                return Unavailable();
            }
        }

        [HttpPost("Collections/{id:int}/Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromRoute] int id, CollectionInputViewModel input, CancellationToken cancellationToken = default)
        {
            string? userId = CurrentUserId();
            if (userId == null) return Challenge();
            try
            {
                // Check ownership before returning even validation errors for a guessed ID.
                if (await collections.OwnedAsync(userId, id, cancellationToken) == null) return NotFound();
                if (!ModelState.IsValid || string.IsNullOrWhiteSpace(input.Name))
                {
                    ViewData["CollectionId"] = id;
                    return View(input);
                }
                if (!await collections.EditAsync(userId, id, input, cancellationToken)) return NotFound();
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception exception) when (IsDatabaseFailure(exception))
            {
                return Unavailable();
            }
        }

        [HttpPost("Collections/{id:int}/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken = default)
        {
            string? userId = CurrentUserId();
            if (userId == null) return Challenge();
            try
            {
                if (!await collections.DeleteAsync(userId, id, cancellationToken)) return NotFound();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception exception) when (IsDatabaseFailure(exception))
            {
                return Unavailable();
            }
        }

        [HttpPost("Collections/{id:int}/Recipes/{recipeId:int}/Add")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Add([FromRoute] int id, [FromRoute] int recipeId, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            return SetMembershipAsync(id, recipeId, true, returnUrl, cancellationToken);
        }

        [HttpPost("Collections/{id:int}/Recipes/{recipeId:int}/Remove")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Remove([FromRoute] int id, [FromRoute] int recipeId, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            return SetMembershipAsync(id, recipeId, false, returnUrl, cancellationToken);
        }

        private async Task<IActionResult> SetMembershipAsync(int id, int recipeId, bool added, string? returnUrl, CancellationToken cancellationToken)
        {
            string? userId = CurrentUserId();
            if (userId == null) return Challenge();
            try
            {
                if (await collections.SetMembershipAsync(userId, id, recipeId, added, cancellationToken) == CollectionMutationResult.NotFound)
                    return NotFound();
                string next = SafeReturnUrl(returnUrl) ?? Url.Action(nameof(Chooser), new { recipeId })!;
                return IsEnhancedRequest()
                    ? Json(new { collectionId = id, recipeId, inCollection = added, message = added ? "Đã thêm vào bộ sưu tập." : "Đã gỡ khỏi bộ sưu tập.", next })
                    : LocalRedirect(next);
            }
            catch (Exception exception) when (IsDatabaseFailure(exception))
            {
                return Unavailable();
            }
        }

        private string? CurrentUserId()
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(userId) ? userId : null;
        }

        private bool IsEnhancedRequest()
        {
            return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.Ordinal);
        }

        private string? SafeReturnUrl(string? returnUrl)
        {
            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        }

        private static bool IsDatabaseFailure(Exception exception)
        {
            return exception is DbUpdateException or DbException;
        }

        private ObjectResult Unavailable()
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Chưa thể cập nhật bộ sưu tập. Vui lòng thử lại." });
        }
    }
}
