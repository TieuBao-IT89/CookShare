using System.Data.Common;
using System.Security.Claims;
using CookShare.Data;
using CookShare.Models;
using CookShare.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CookShare.Controllers
{
    [Route("Recipe/{recipeId:int}/Likes")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class RecipeLikesController : Controller
    {
        private static readonly Action<ILogger, string, Exception?> _logFailure =
            LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1076, "RecipeLikeFailure"),
                "Recipe like request failed with {FailureType}.");

        private readonly ApplicationDbContext _db;
        private readonly ILogger<RecipeLikesController> _logger;

        public RecipeLikesController(ApplicationDbContext db, ILogger<RecipeLikesController>? logger = null)
        {
            _db = db;
            _logger = logger ?? NullLogger<RecipeLikesController>.Instance;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromRoute] int recipeId)
        {
            try
            {
                var state = await _db.Recipes.AsNoTracking()
                    .Where(recipe => recipe.Id == recipeId)
                    .SelectLikeStates(User.Identity?.IsAuthenticated == true
                        ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null)
                    .SingleOrDefaultAsync();

                return state == null
                    ? NotFound(new { message = "Không tìm thấy công thức." })
                    : Json(state);
            }
            catch (DbException exception)
            {
                return DatabaseUnavailable(exception);
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Like([FromRoute] int recipeId)
        {
            return SetLikeAsync(recipeId, true);
        }

        [Authorize]
        [HttpPost("Unlike")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Unlike([FromRoute] int recipeId)
        {
            return SetLikeAsync(recipeId, false);
        }

        private async Task<IActionResult> SetLikeAsync(int recipeId, bool liked)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (User.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            try
            {
                var targetRecipe = await _db.Recipes.FirstOrDefaultAsync(item => item.Id == recipeId);
                if (targetRecipe == null)
                {
                    return NotFound(new { message = "Không tìm thấy công thức." });
                }

                var existing = await _db.RecipeLikes
                    .SingleOrDefaultAsync(like => like.RecipeId == recipeId && like.UserId == userId);
                RecipeLike? changed = null;
                if (liked && existing == null)
                {
                    changed = new RecipeLike { RecipeId = recipeId, Recipe = targetRecipe, UserId = userId, CreatedAt = DateTime.UtcNow };
                    _db.RecipeLikes.Add(changed);
                }
                else if (!liked && existing != null)
                {
                    changed = existing;
                    _db.RecipeLikes.Remove(existing);
                }

                if (changed != null)
                {
                    // Keep the interaction and its notification in one PostgreSQL transaction.
                    await using var transaction = liked && _db.Database.IsRelational()
                        ? await _db.Database.BeginTransactionAsync()
                        : null;
                    try
                    {
                        await _db.SaveChangesAsync();
                        if (liked)
                        {
                            await new NotificationService(_db).NotifyRecipeLikeAsync(changed);
                        }

                        if (transaction != null)
                        {
                            await transaction.CommitAsync();
                        }
                    }
                    catch (DbUpdateException exception) when (liked && IsDuplicateLike(exception))
                    {
                        // The unique index arbitrates simultaneous likes from the same user.
                        if (transaction != null)
                        {
                            await transaction.RollbackAsync();
                        }

                        _db.Entry(changed).State = EntityState.Detached;
                    }
                    catch (DbUpdateConcurrencyException) when (!liked)
                    {
                        // Another request already removed this user's like.
                        _db.Entry(changed).State = EntityState.Detached;
                    }
                    catch (DbUpdateException exception) when (
                        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
                    {
                        if (transaction != null)
                        {
                            await transaction.RollbackAsync();
                        }

                        _db.Entry(changed).State = EntityState.Detached;
                        if (!await _db.Recipes.AnyAsync(recipe => recipe.Id == recipeId))
                        {
                            return NotFound(new { message = "Không tìm thấy công thức." });
                        }

                        throw;
                    }
                }

                var state = await _db.Recipes.AsNoTracking()
                    .Where(recipe => recipe.Id == recipeId)
                    .SelectLikeStates(userId)
                    .SingleOrDefaultAsync();
                if (state == null)
                {
                    return NotFound(new { message = "Không tìm thấy công thức." });
                }

                return Json(new
                {
                    state.RecipeId,
                    state.LikeCount,
                    state.IsLiked,
                    message = state.IsLiked ? "Đã thích công thức." : "Đã bỏ thích công thức."
                });
            }
            catch (Exception exception) when (exception is DbUpdateException or DbException)
            {
                return DatabaseUnavailable(exception);
            }
        }

        private static bool IsDuplicateLike(DbUpdateException exception)
        {
            return exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_RecipeLikes_UserId_RecipeId"
            };
        }

        private ObjectResult DatabaseUnavailable(Exception exception)
        {
            _logFailure(_logger, exception.GetType().Name, null);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Chưa thể cập nhật lượt thích. Vui lòng thử lại sau." });
        }
    }
}
