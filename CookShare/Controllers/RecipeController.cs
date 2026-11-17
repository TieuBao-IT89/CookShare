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
using Microsoft.Extensions.Logging.Abstractions;

namespace CookShare.Controllers
{
    public class RecipeController : Controller
    {
        private static readonly Action<ILogger, string, string, Exception?> _logSaveFailure =
            LoggerMessage.Define<string, string>(
                LogLevel.Warning,
                new EventId(1001, "RecipeSaveFailure"),
                "Recipe {Operation} failed with {FailureType}.");

        private readonly ApplicationDbContext _db;
        private readonly ILogger<RecipeController> _logger;
        private readonly IRecipeImageStorage? _imageStorage;
        private readonly ISocialImageStorage? _socialImages;

        public RecipeController(ApplicationDbContext db, ILogger<RecipeController>? logger = null,
            IRecipeImageStorage? imageStorage = null, ISocialImageStorage? socialImages = null)
        {
            _db = db;
            _logger = logger ?? NullLogger<RecipeController>.Instance;
            _imageStorage = imageStorage;
            _socialImages = socialImages;
        }

        // GET: /Recipe
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index(
            string? q = null,
            string? category = null,
            RecipeDifficulty? difficulty = null,
            int? maxTotalMinutes = null,
            string? ingredient = null, string? author = null, string sort = "newest", string? cursor = null)
        {
            if (!ModelState.IsValid
                || (difficulty.HasValue && !Enum.IsDefined(difficulty.Value))
                || (maxTotalMinutes.HasValue && maxTotalMinutes is < 1 or > 2880))
            {
                return BadRequest();
            }

            string query = q?.Trim() ?? string.Empty;
            string selectedCategory = category?.Trim() ?? string.Empty;
            var options = new DiscoveryOptions(query, ingredient?.Trim() ?? string.Empty, author?.Trim() ?? string.Empty,
                selectedCategory, difficulty, maxTotalMinutes, sort);
            if (sort is not ("newest" or "popular") || query.Length > 200 || options.Ingredient.Length > 200
                || options.Author.Length > 100 || selectedCategory.Length > 100
                || !DiscoveryCursor.TryParse(cursor, options, out var position))
            {
                return BadRequest();
            }
            var page = await SocialDiscoveryQueries.LoadAsync(_db, options, position, HttpContext.RequestAborted);
            var recipes = page.Recipes;

            ViewData["RecipeLikeSummaries"] = await RecipeLikeQueries.LoadAsync(
                _db, recipes.Select(recipe => recipe.Id), User?.FindFirstValue(ClaimTypes.NameIdentifier));
            ViewData["RecipeRatingSummaries"] = await RecipeRatingQueries.LoadAsync(
                _db, recipes.Select(recipe => recipe.Id), User?.FindFirstValue(ClaimTypes.NameIdentifier));
            ViewData["SavedRecipeIds"] = await FavoriteQueries.LoadSavedIdsAsync(
                _db, recipes.Select(recipe => recipe.Id), User?.FindFirstValue(ClaimTypes.NameIdentifier));

            return View(new RecipeIndexViewModel
            {
                Recipes = recipes,
                Query = query,
                Category = selectedCategory,
                Difficulty = difficulty,
                MaxTotalMinutes = maxTotalMinutes,
                Ingredient = options.Ingredient,
                Author = options.Author,
                Sort = sort,
                NextCursor = page.NextCursor,
                TotalCount = page.TotalCount
            });
        }

        // GET: /Recipe/Details/5
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Details(int id)
        {
            var recipe = await _db.Recipes
                .AsNoTracking()
                .AsSplitQuery()
                .Include(r => r.User)
                .Include(r => r.Likes)
                .Include(r => r.Comments)
                    .ThenInclude(comment => comment.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
            {
                return NotFound();
            }

            string? userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var related = await RecipeDiscoveryQueries.RelatedAsync(_db, recipe.Id, recipe.Category);
            var displayedIds = related.Select(item => item.Id).Prepend(recipe.Id);
            ViewData["RelatedRecipes"] = related;
            ViewData["CookedPosts"] = await SocialPostQueries.Cards(_db, userId).Where(post => post.RecipeId == id)
                .OrderByDescending(post => post.CreatedAt).ThenByDescending(post => post.Id).Take(6).ToListAsync();
            ViewData["RecipeLikeSummaries"] = await RecipeLikeQueries.LoadAsync(_db, displayedIds, userId);
            ViewData["RecipeRatingSummaries"] = await RecipeRatingQueries.LoadAsync(_db, displayedIds, userId);
            ViewData["SavedRecipeIds"] = await FavoriteQueries.LoadSavedIdsAsync(_db, displayedIds, userId);
            ViewData["RecipeComments"] = new RecipeCommentsViewModel
            {
                RecipeId = recipe.Id,
                Comments = await RecipeCommentQueries.LoadAsync(_db, recipe.Id, userId),
                IsAuthenticated = User?.Identity?.IsAuthenticated == true
            };

            return View(recipe);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Like(int id)
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var recipe = await _db.Recipes
                .Include(item => item.Likes)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (recipe == null)
            {
                return NotFound();
            }

            var existingLike = recipe.Likes
                .FirstOrDefault(like => like.UserId == userId);

            if (existingLike != null)
            {
                _db.RecipeLikes.Remove(existingLike);
                await _db.SaveChangesAsync();
            }
            else
            {
                await using var transaction =
                    await _db.Database.BeginTransactionAsync();

                var like = new RecipeLike
                {
                    UserId = userId,
                    RecipeId = recipe.Id,
                    Recipe = recipe,
                    CreatedAt = DateTime.UtcNow
                };

                _db.RecipeLikes.Add(like);
                await _db.SaveChangesAsync();

                await new NotificationService(_db)
                    .NotifyRecipeLikeAsync(like);

                await transaction.CommitAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(
            int id,
            string content)
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var recipe = await _db.Recipes
                .FirstOrDefaultAsync(item => item.Id == id);

            if (recipe == null)
            {
                return NotFound();
            }

            var trimmedContent = content?.Trim();
            if (string.IsNullOrEmpty(trimmedContent)
                || trimmedContent.Length > 1000)
            {
                TempData["Error"] =
                    "Bình luận cần có nội dung và không vượt quá 1000 ký tự.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await using var transaction =
                await _db.Database.BeginTransactionAsync();

            var comment = new RecipeComment
            {
                UserId = userId,
                RecipeId = recipe.Id,
                Recipe = recipe,
                Content = trimmedContent,
                CreatedAt = DateTime.UtcNow
            };

            _db.RecipeComments.Add(comment);
            await _db.SaveChangesAsync();

            await new NotificationService(_db)
                .NotifyRecipeCommentAsync(comment);

            await transaction.CommitAsync();

            return RedirectToAction(
                nameof(Details),
                "Recipe",
                new { id },
                $"comment-{comment.Id}");
        }

        // GET: /Recipe/Create
        [Authorize]
        [HttpGet]
        public IActionResult Create()
        {
            return View(new RecipeFormViewModel());
        }

        // POST: /Recipe/Create
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
        [EnableRateLimiting("cooked-upload")]
        public async Task<IActionResult> Create(
            RecipeFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            string? uploadedImage = await SaveUploadedImageAsync(model);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var recipe = new Recipe
            {
                Name = model.Name.Trim(),
                Description = model.Description.Trim(),
                Ingredients = model.Ingredients.Trim(),
                Instructions = model.Instructions.Trim(),
                PreparationTimeMinutes = model.PreparationTimeMinutes,
                CookingTimeMinutes = model.CookingTimeMinutes,
                Difficulty = model.Difficulty,
                Servings = model.Servings,
                Category = model.Category.Trim(),
                Image = uploadedImage ?? (string.IsNullOrWhiteSpace(model.Image) ? null : model.Image.Trim()),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _db.Recipes.Add(recipe);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                await DeleteFailedUploadAsync(uploadedImage);
                if (exception is not (DbUpdateException or DbException))
                {
                    throw;
                }

                _logSaveFailure(_logger, "creation", exception.GetType().Name, null);
                _db.Entry(recipe).State = EntityState.Detached;
                ModelState.AddModelError(string.Empty, "Chưa thể lưu công thức. Vui lòng thử lại sau.");
                return View(model);
            }

            TempData["Success"] = "Đã tạo công thức thành công.";

            return RedirectToAction(
                nameof(Details),
                new { id = recipe.Id });
        }

        // GET: /Recipe/Edit/5
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var recipe = await _db.Recipes
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId) || recipe.UserId != userId)
            {
                return Forbid();
            }

            ViewBag.RecipeId = id;

            var model = new RecipeFormViewModel
            {
                Name = recipe.Name,
                Description = recipe.Description,
                Ingredients = recipe.Ingredients,
                Instructions = recipe.Instructions,
                PreparationTimeMinutes = recipe.PreparationTimeMinutes,
                CookingTimeMinutes = recipe.CookingTimeMinutes,
                Difficulty = recipe.Difficulty,
                Servings = recipe.Servings,
                Category = recipe.Category,
                Image = recipe.Image
            };

            return View(model);
        }

        // POST: /Recipe/Edit/5
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
        [EnableRateLimiting("cooked-upload")]
        public async Task<IActionResult> Edit(
            int id,
            RecipeFormViewModel model)
        {
            var recipe = await _db.Recipes
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId) || recipe.UserId != userId)
            {
                return Forbid();
            }

            ViewBag.RecipeId = id;

            // Submitting without another file keeps the stored photo, including legacy references.
            if (string.IsNullOrWhiteSpace(model.Image))
            {
                model.Image = recipe.Image;
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string? uploadedImage = await SaveUploadedImageAsync(model);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string? previousImage = recipe.Image;
            recipe.Name = model.Name.Trim();
            recipe.Description = model.Description.Trim();
            recipe.Ingredients = model.Ingredients.Trim();
            recipe.Instructions = model.Instructions.Trim();
            recipe.PreparationTimeMinutes = model.PreparationTimeMinutes;
            recipe.CookingTimeMinutes = model.CookingTimeMinutes;
            recipe.Difficulty = model.Difficulty;
            recipe.Servings = model.Servings;
            recipe.Category = model.Category.Trim();
            recipe.Image = uploadedImage ?? (string.IsNullOrWhiteSpace(model.Image) ? null : model.Image.Trim());

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                await DeleteFailedUploadAsync(uploadedImage);
                if (exception is not (DbUpdateException or DbException))
                {
                    throw;
                }

                _logSaveFailure(_logger, "update", exception.GetType().Name, null);
                _db.Entry(recipe).State = EntityState.Detached;
                ModelState.AddModelError(string.Empty, "Chưa thể cập nhật công thức. Vui lòng thử lại sau.");
                return View(model);
            }

            if (previousImage != recipe.Image)
            {
                await DeleteImageIfUnreferencedAsync(previousImage);
            }
            TempData["Success"] = "Đã cập nhật công thức.";

            return RedirectToAction(
                nameof(Details),
                new { id = recipe.Id });
        }

        private async Task<string?> SaveUploadedImageAsync(RecipeFormViewModel model)
        {
            if (model.ImageFile == null)
            {
                return null;
            }

            if (_imageStorage == null)
            {
                ModelState.AddModelError(nameof(model.ImageFile), "Chưa thể tải ảnh lên. Vui lòng thử lại sau.");
                return null;
            }

            try
            {
                return await _imageStorage.SaveAsync(model.ImageFile, HttpContext.RequestAborted);
            }
            catch (RecipeImageUploadException exception)
            {
                ModelState.AddModelError(nameof(model.ImageFile), exception.Message);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logSaveFailure(_logger, "image upload", exception.GetType().Name, null);
                ModelState.AddModelError(nameof(model.ImageFile), "Chưa thể tải ảnh lên. Vui lòng thử lại sau.");
            }

            return null;
        }

        private async Task DeleteFailedUploadAsync(string? imageReference)
        {
            if (imageReference == null || _imageStorage == null)
            {
                return;
            }

            try
            {
                await _imageStorage.DeleteAsync(imageReference);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logSaveFailure(_logger, "image cleanup", exception.GetType().Name, null);
            }
        }

        private async Task DeleteImageIfUnreferencedAsync(string? reference)
        {
            if (reference != null && !await _db.Recipes.IgnoreQueryFilters().AnyAsync(recipe => recipe.Image == reference))
            {
                await DeleteFailedUploadAsync(reference);
            }
        }

        // POST: /Recipe/Delete/5
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var recipe = await _db.Recipes
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId) || recipe.UserId != userId)
            {
                return Forbid();
            }

            var cookedImages = await _db.CookedRecipePosts.IgnoreQueryFilters().Where(post => post.RecipeId == id)
                .Select(post => post.ImageFileName).ToListAsync();
            _db.Recipes.Remove(recipe);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception exception) when (exception is DbUpdateException or DbException)
            {
                _logSaveFailure(_logger, "deletion", exception.GetType().Name, null);
                _db.Entry(recipe).State = EntityState.Unchanged;
                TempData["Error"] = "Chưa thể xóa công thức. Vui lòng thử lại sau.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await DeleteImageIfUnreferencedAsync(recipe.Image);
            if (_socialImages != null)
            {
                foreach (string file in cookedImages)
                {
                    try { await _socialImages.DeleteAsync(file); }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        _logSaveFailure(_logger, "cooked image cleanup", exception.GetType().Name, null);
                    }
                }
            }
            TempData["Success"] = "Đã xóa công thức.";

            return RedirectToAction(nameof(Index));
        }

        // GET: /Recipe/Saved
        [Authorize]
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Saved()
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var recipes = await _db.Favorites
                .AsNoTracking()
                .Where(favorite => favorite.UserId == userId)
                .OrderByDescending(favorite => favorite.CreatedAt)
                .ThenByDescending(favorite => favorite.Id)
                .Include(favorite => favorite.Recipe)
                .ThenInclude(recipe => recipe.User)
                .Select(favorite => favorite.Recipe)
                .ToListAsync();

            ViewData["SavedRecipeIds"] = recipes.Select(recipe => recipe.Id).ToHashSet();
            ViewData["RecipeLikeSummaries"] = await RecipeLikeQueries.LoadAsync(
                _db, recipes.Select(recipe => recipe.Id), userId);
            ViewData["RecipeRatingSummaries"] = await RecipeRatingQueries.LoadAsync(
                _db, recipes.Select(recipe => recipe.Id), userId);
            return View(recipes);
        }
    }
}
