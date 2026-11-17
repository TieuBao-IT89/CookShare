using System.Data.Common;
using System.Security.Claims;
using CookShare.Data;
using CookShare.Models;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CookShare.Controllers
{
    [Route("Recipe/{recipeId:int}/Ratings")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class RecipeRatingsController : Controller
    {
        private static readonly Action<ILogger, string, Exception?> _logFailure =
            LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1096, "RecipeRatingFailure"),
                "Recipe rating request failed with {FailureType}.");
        private readonly ApplicationDbContext _db;
        private readonly ILogger<RecipeRatingsController> _logger;

        public RecipeRatingsController(ApplicationDbContext db, ILogger<RecipeRatingsController>? logger = null)
        {
            _db = db;
            _logger = logger ?? NullLogger<RecipeRatingsController>.Instance;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromRoute] int recipeId, CancellationToken cancellationToken)
        {
            try
            {
                var state = await StateAsync(recipeId, CurrentUserId(), cancellationToken);
                return state == null ? RecipeNotFound() : Json(state);
            }
            catch (DbException exception)
            {
                return DatabaseUnavailable(exception);
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(
            [FromRoute] int recipeId, RecipeRatingFormViewModel input, CancellationToken cancellationToken)
        {
            string? viewerId = CurrentUserId();
            if (viewerId == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid || input.Stars is < 1 or > 5)
            {
                return BadRequest(new { message = "Vui lòng chọn số sao nguyên từ 1 đến 5." });
            }

            try
            {
                var recipe = await _db.Recipes.AsNoTracking().Where(item => item.Id == recipeId)
                    .Select(item => new { item.UserId }).SingleOrDefaultAsync(cancellationToken);
                if (recipe == null)
                {
                    return RecipeNotFound();
                }

                if (recipe.UserId == viewerId)
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new { message = "Bạn không thể tự đánh giá công thức của mình." });
                }

                var rating = await _db.RecipeRatings.SingleOrDefaultAsync(
                    item => item.RecipeId == recipeId && item.UserId == viewerId, cancellationToken);
                bool creating = rating == null;
                if (rating == null)
                {
                    rating = new RecipeRating { RecipeId = recipeId, UserId = viewerId, Stars = input.Stars };
                    _db.RecipeRatings.Add(rating);
                }
                else
                {
                    rating.Stars = input.Stars;
                    _db.Entry(rating).Property(item => item.Stars).IsModified = true;
                }

                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException exception) when (creating && exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "IX_RecipeRatings_UserId_RecipeId"
                })
                {
                    // Another request created this account's rating; update that row instead of adding a second one.
                    _db.Entry(rating).State = EntityState.Detached;
                    var winner = await _db.RecipeRatings.SingleOrDefaultAsync(
                        item => item.RecipeId == recipeId && item.UserId == viewerId, cancellationToken);
                    if (winner == null)
                    {
                        return RecipeNotFound();
                    }

                    winner.Stars = input.Stars;
                    _db.Entry(winner).Property(item => item.Stars).IsModified = true;
                    await _db.SaveChangesAsync(cancellationToken);
                }

                var state = await StateAsync(recipeId, viewerId, cancellationToken);
                if (state == null)
                {
                    return RecipeNotFound();
                }

                if (Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
                    || Request.Headers.XRequestedWith == "XMLHttpRequest")
                {
                    return Json(state);
                }

                TempData["Success"] = "Đã lưu đánh giá của bạn.";
                return RedirectToAction("Details", "Recipe", new { id = recipeId }, "recipeRatingSection");
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ForeignKeyViolation })
            {
                return !await _db.Recipes.AsNoTracking().AnyAsync(item => item.Id == recipeId, cancellationToken)
                    ? RecipeNotFound() : DatabaseUnavailable(exception);
            }
            catch (Exception exception) when (exception is DbUpdateException or DbException)
            {
                return DatabaseUnavailable(exception);
            }
        }

        private string? CurrentUserId()
        {
            return User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        }

        private Task<RecipeRatingViewModel?> StateAsync(int recipeId, string? viewerId, CancellationToken cancellationToken)
        {
            return _db.Recipes.AsNoTracking().Where(recipe => recipe.Id == recipeId)
                .SelectRatingStates(viewerId).SingleOrDefaultAsync(cancellationToken);
        }

        private NotFoundObjectResult RecipeNotFound()
        {
            return NotFound(new { message = "Không tìm thấy công thức." });
        }

        private ObjectResult DatabaseUnavailable(Exception exception)
        {
            _logFailure(_logger, exception.GetType().Name, null);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Chưa thể lưu đánh giá. Vui lòng thử lại sau." });
        }
    }
}
