using System.Data.Common;
using System.Security.Claims;
using CookShare.Data;
using CookShare.Models;
using CookShare.Services;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CookShare.Controllers
{
    [Route("Recipe/{recipeId:int}/Comments")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class RecipeCommentsController : Controller
    {
        private static readonly Action<ILogger, string, string, Exception?> _logFailure =
            LoggerMessage.Define<string, string>(
                LogLevel.Warning,
                new EventId(1201, "RecipeCommentFailure"),
                "Recipe comment {Operation} failed with {FailureType}.");

        private readonly ApplicationDbContext _db;
        private readonly ILogger<RecipeCommentsController> _logger;

        public RecipeCommentsController(ApplicationDbContext db, ILogger<RecipeCommentsController>? logger = null)
        {
            _db = db;
            _logger = logger ?? NullLogger<RecipeCommentsController>.Instance;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromRoute] int recipeId)
        {
            try
            {
                if (!await RecipeExistsAsync(recipeId))
                {
                    return MissingRecipe();
                }

                string? userId = User.Identity?.IsAuthenticated == true
                    ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                    : null;
                List<RecipeCommentViewModel> comments = await RecipeCommentQueries.LoadAsync(_db, recipeId, userId);
                return Json(new { recipeId, count = comments.Count, comments });
            }
            catch (DbException exception)
            {
                return DatabaseFailure("listing", exception);
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromRoute] int recipeId, [FromForm] CommentFormViewModel model)
        {
            string? userId = CurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            IActionResult? validationError = ValidateContent(model);
            if (validationError != null)
            {
                return validationError;
            }

            RecipeComment? comment = null;
            try
            {
                var recipe = await _db.Recipes.FirstOrDefaultAsync(item => item.Id == recipeId);
                if (recipe == null)
                {
                    return MissingRecipe();
                }

                if (!await _db.Users.AsNoTracking().AnyAsync(user => user.Id == userId))
                {
                    return Challenge();
                }

                comment = new RecipeComment
                {
                    RecipeId = recipeId,
                    Recipe = recipe,
                    UserId = userId,
                    Content = model.Content,
                    CreatedAt = DateTime.UtcNow
                };
                await using var transaction = _db.Database.IsRelational()
                    ? await _db.Database.BeginTransactionAsync()
                    : null;
                _db.RecipeComments.Add(comment);
                await _db.SaveChangesAsync();
                await new NotificationService(_db).NotifyRecipeCommentAsync(comment);
                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return await SavedCommentAsync(comment, userId, "Đã đăng bình luận.");
            }
            catch (DbUpdateException exception)
            {
                if (comment != null)
                {
                    _db.Entry(comment).State = EntityState.Detached;
                }

                return await SaveFailureAsync("creation", exception, recipeId);
            }
            catch (DbException exception)
            {
                return DatabaseFailure("creation", exception);
            }
        }

        [Authorize]
        [HttpPost("{commentId:int}/Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromRoute] int recipeId, [FromRoute] int commentId, [FromForm] CommentFormViewModel model)
        {
            string? userId = CurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            RecipeComment? comment = null;
            try
            {
                comment = await _db.RecipeComments.FirstOrDefaultAsync(item =>
                    item.Id == commentId && item.RecipeId == recipeId);
                if (comment == null)
                {
                    return MissingComment();
                }

                if (comment.UserId != userId)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn chỉ có thể sửa bình luận của mình." });
                }

                IActionResult? validationError = ValidateContent(model);
                if (validationError != null)
                {
                    return validationError;
                }

                comment.Content = model.Content;
                await _db.SaveChangesAsync();
                return await SavedCommentAsync(comment, userId, "Đã cập nhật bình luận.");
            }
            catch (DbUpdateException exception)
            {
                if (comment != null)
                {
                    _db.Entry(comment).State = EntityState.Detached;
                }

                return await SaveFailureAsync("update", exception, recipeId, commentId);
            }
            catch (DbException exception)
            {
                return DatabaseFailure("update", exception);
            }
        }

        [Authorize]
        [HttpPost("{commentId:int}/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromRoute] int recipeId, [FromRoute] int commentId)
        {
            string? userId = CurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            RecipeComment? comment = null;
            try
            {
                comment = await _db.RecipeComments.FirstOrDefaultAsync(item =>
                    item.Id == commentId && item.RecipeId == recipeId);
                if (comment == null)
                {
                    return MissingComment();
                }

                if (comment.UserId != userId)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn chỉ có thể xóa bình luận của mình." });
                }

                _db.RecipeComments.Remove(comment);
                await _db.SaveChangesAsync();
                int count = await _db.RecipeComments.CountAsync(item => item.RecipeId == recipeId);
                return Json(new { commentId, count, message = "Đã xóa bình luận." });
            }
            catch (DbUpdateException exception)
            {
                if (comment != null)
                {
                    _db.Entry(comment).State = EntityState.Detached;
                }

                return await SaveFailureAsync("deletion", exception, recipeId, commentId);
            }
            catch (DbException exception)
            {
                return DatabaseFailure("deletion", exception);
            }
        }

        private string? CurrentUserId()
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(userId) ? userId : null;
        }

        private Task<bool> RecipeExistsAsync(int recipeId)
        {
            return _db.Recipes.AsNoTracking().AnyAsync(recipe => recipe.Id == recipeId);
        }

        private BadRequestObjectResult? ValidateContent(CommentFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Content))
            {
                ModelState.TryAddModelError(nameof(model.Content), "Vui lòng nhập nội dung bình luận.");
            }
            else if (model.Content.Length > CommentFormViewModel.MaxLength)
            {
                ModelState.TryAddModelError(nameof(model.Content), "Bình luận không được vượt quá 500 ký tự.");
            }

            if (ModelState.IsValid)
            {
                return null;
            }

            return BadRequest(new
            {
                message = "Vui lòng nhập bình luận từ 1 đến 500 ký tự.",
                errors = ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors
                        .Select(error => string.IsNullOrEmpty(error.ErrorMessage) ? "Nội dung không hợp lệ." : error.ErrorMessage)
                        .ToArray())
            });
        }

        private async Task<IActionResult> SavedCommentAsync(RecipeComment saved, string userId, string message)
        {
            RecipeCommentViewModel? comment = await RecipeCommentQueries.ForRecipe(_db, saved.RecipeId, userId)
                .FirstOrDefaultAsync(item => item.Id == saved.Id);
            if (comment == null)
            {
                return MissingComment();
            }

            int count = await _db.RecipeComments.CountAsync(item => item.RecipeId == saved.RecipeId);
            return Json(new { comment, count, message });
        }

        private async Task<IActionResult> SaveFailureAsync(string operation, DbUpdateException exception, int recipeId, int? commentId = null)
        {
            try
            {
                // A recipe/comment can disappear after the initial read because another request deleted it.
                if (!await RecipeExistsAsync(recipeId))
                {
                    return MissingRecipe();
                }

                if (commentId.HasValue && !await _db.RecipeComments.AsNoTracking()
                    .AnyAsync(comment => comment.Id == commentId.Value && comment.RecipeId == recipeId))
                {
                    return MissingComment();
                }
            }
            catch (DbException)
            {
                // Report the original operation safely if the database is still unavailable.
            }

            return DatabaseFailure(operation, exception);
        }

        private ObjectResult DatabaseFailure(string operation, Exception exception)
        {
            _logFailure(_logger, operation, exception.GetType().Name, null);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Chưa thể tải hoặc lưu bình luận. Vui lòng thử lại sau."
            });
        }

        private NotFoundObjectResult MissingRecipe()
        {
            return NotFound(new { message = "Không tìm thấy công thức." });
        }

        private NotFoundObjectResult MissingComment()
        {
            return NotFound(new { message = "Không tìm thấy bình luận." });
        }
    }
}
