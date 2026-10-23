using System.Data.Common;
using System.Security.Claims;
using CookShare.Data;
using CookShare.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CookShare.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class RecipeFavoritesController : Controller
    {
        private static readonly Action<ILogger, string, Exception?> _logFailure =
            LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1094, "RecipeFavoriteFailure"),
                "Recipe favorite request failed with {FailureType}.");

        private readonly ApplicationDbContext _db;
        private readonly ILogger<RecipeFavoritesController> _logger;

        public RecipeFavoritesController(ApplicationDbContext db, ILogger<RecipeFavoritesController>? logger = null)
        {
            _db = db;
            _logger = logger ?? NullLogger<RecipeFavoritesController>.Instance;
        }

        [HttpGet("Recipe/Favorites/State")]
        public async Task<IActionResult> State([FromQuery] int[]? recipeIds)
        {
            string? userId = CurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            try
            {
                int[] ids = recipeIds?.Where(id => id > 0).Distinct().ToArray() ?? Array.Empty<int>();
                var favorites = _db.Favorites.AsNoTracking().Where(favorite => favorite.UserId == userId);
                int[] savedRecipeIds = await favorites
                    .Where(favorite => ids.Contains(favorite.RecipeId))
                    .Select(favorite => favorite.RecipeId)
                    .ToArrayAsync();

                return Json(new { userId, savedRecipeIds, savedCount = await favorites.CountAsync() });
            }
            catch (Exception exception) when (exception is DbUpdateException or DbException)
            {
                return DatabaseUnavailable(exception);
            }
        }

        [HttpPost("Recipe/{recipeId:int}/Favorites")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Save([FromRoute] int recipeId)
        {
            return SetSavedAsync(recipeId, true);
        }

        [HttpPost("Recipe/{recipeId:int}/Favorites/Unsave")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Unsave([FromRoute] int recipeId)
        {
            return SetSavedAsync(recipeId, false);
        }

        private string? CurrentUserId()
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(userId) ? userId : null;
        }

        private async Task<IActionResult> SetSavedAsync(int recipeId, bool saved)
        {
            string? userId = CurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            try
            {
                if (!await _db.Recipes.AsNoTracking().AnyAsync(recipe => recipe.Id == recipeId))
                {
                    return RecipeNotFound();
                }

                var existing = await _db.Favorites
                    .SingleOrDefaultAsync(favorite => favorite.RecipeId == recipeId && favorite.UserId == userId);
                Favorite? changed = null;
                if (saved && existing == null)
                {
                    changed = new Favorite { RecipeId = recipeId, UserId = userId, CreatedAt = DateTime.UtcNow };
                    _db.Favorites.Add(changed);
                }
                else if (!saved && existing != null)
                {
                    changed = existing;
                    _db.Favorites.Remove(existing);
                }

                if (changed != null)
                {
                    try
                    {
                        await _db.SaveChangesAsync();
                    }
                    catch (DbUpdateException exception) when (saved && IsDuplicateFavorite(exception))
                    {
                        // The existing unique index arbitrates simultaneous saves for this account.
                        _db.Entry(changed).State = EntityState.Detached;
                    }
                    catch (DbUpdateConcurrencyException) when (!saved)
                    {
                        // Another request has already removed this account's favorite.
                        _db.Entry(changed).State = EntityState.Detached;
                    }
                    catch (DbUpdateException exception) when (
                        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
                    {
                        _db.Entry(changed).State = EntityState.Detached;
                        if (!await _db.Recipes.AsNoTracking().AnyAsync(recipe => recipe.Id == recipeId))
                        {
                            return RecipeNotFound();
                        }

                        throw;
                    }
                }

                if (!await _db.Recipes.AsNoTracking().AnyAsync(recipe => recipe.Id == recipeId))
                {
                    return RecipeNotFound();
                }

                var favorites = _db.Favorites.AsNoTracking().Where(favorite => favorite.UserId == userId);
                bool isSaved = await favorites.AnyAsync(favorite => favorite.RecipeId == recipeId);
                return Json(new
                {
                    recipeId,
                    isSaved,
                    savedCount = await favorites.CountAsync(),
                    message = isSaved ? "Đã lưu công thức." : "Đã bỏ lưu công thức."
                });
            }
            catch (Exception exception) when (exception is DbUpdateException or DbException)
            {
                return DatabaseUnavailable(exception);
            }
        }

        private NotFoundObjectResult RecipeNotFound()
        {
            return NotFound(new { message = "Không tìm thấy công thức." });
        }

        private static bool IsDuplicateFavorite(DbUpdateException exception)
        {
            return exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Favorites_UserId_RecipeId"
            };
        }

        private ObjectResult DatabaseUnavailable(Exception exception)
        {
            _logFailure(_logger, exception.GetType().Name, null);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Chưa thể cập nhật công thức đã lưu. Vui lòng thử lại sau." });
        }
    }
}
