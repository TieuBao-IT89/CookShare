
using System.Security.Claims;
using CookShare.Data;
using CookShare.Helpers;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.Services;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET: /Profile
        // GET: /Profile?userId=...
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index(string? userId = null)
        {
            var currentUserId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Challenge();
                }

                userId = currentUserId;
            }

            var profileUser = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (profileUser == null)
            {
                return NotFound();
            }

            bool isOwner =
                !string.IsNullOrEmpty(currentUserId)
                && currentUserId == profileUser.Id;

            var recipes = await _db.Recipes
                .AsNoTracking()
                .Where(r => r.UserId == profileUser.Id)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .ToListAsync();

            var followersCount = await _db.Follows
                .CountAsync(f => f.FollowingId == profileUser.Id);

            var followingCount = await _db.Follows
                .CountAsync(f => f.FollowerId == profileUser.Id);

            var isFollowing =
                !isOwner
                && !string.IsNullOrEmpty(currentUserId)
                && await _db.Follows.AnyAsync(f =>
                    f.FollowerId == currentUserId
                    && f.FollowingId == profileUser.Id);

            var followers = await _db.Follows
                .AsNoTracking()
                .Where(f => f.FollowingId == profileUser.Id)
                .Select(f => new ProfileUserItemViewModel
                {
                    UserId = f.Follower.Id,
                    UserName = f.Follower.UserName ?? "",
                    DisplayName =
                        f.Follower.DisplayName
                        ?? f.Follower.UserName
                        ?? "CookShare User",
                    AvatarUrl = f.Follower.AvatarUrl
                })
                .ToListAsync();

            var following = await _db.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == profileUser.Id)
                .Select(f => new ProfileUserItemViewModel
                {
                    UserId = f.Following.Id,
                    UserName = f.Following.UserName ?? "",
                    DisplayName =
                        f.Following.DisplayName
                        ?? f.Following.UserName
                        ?? "CookShare User",
                    AvatarUrl = f.Following.AvatarUrl
                })
                .ToListAsync();

            var featuredPins = await _db.FeaturedRecipes
                .AsNoTracking()
                .Where(pin => pin.UserId == profileUser.Id)
                .OrderBy(pin => pin.SortOrder)
                .ThenBy(pin => pin.Id)
                .Include(pin => pin.Recipe)
                    .ThenInclude(recipe => recipe.User)
                .ToListAsync();
            var featured = featuredPins.Select(pin => pin.Recipe).ToList();

            var favorites = isOwner
                ? await _db.Favorites
                    .AsNoTracking()
                    .Where(f => f.UserId == profileUser.Id)
                    .OrderByDescending(f => f.CreatedAt)
                    .ThenByDescending(f => f.Id)
                    .Include(f => f.Recipe)
                    .ThenInclude(r => r.User)
                    .Select(f => f.Recipe)
                    .ToListAsync()
                : new List<CookShare.Models.Recipe>();

            var model = new ProfileViewModel
            {
                UserId = profileUser.Id,
                UserName = profileUser.UserName ?? "",
                DisplayName =
                    profileUser.DisplayName
                    ?? profileUser.UserName
                    ?? "CookShare User",
                AvatarUrl = profileUser.AvatarUrl,
                CoverImageUrl = profileUser.CoverImageUrl,
                Bio = profileUser.Bio,
                JoinedAt = profileUser.CreatedAt,
                IsOwner = isOwner,
                IsFollowing = isFollowing,
                RecipeCount = recipes.Count,
                FollowersCount = followersCount,
                FollowingCount = followingCount,
                FeaturedRecipes = featured,
                Recipes = recipes,
                Followers = followers,
                Following = following,
                Favorites = favorites
            };

            var displayedIds = featured
                .Concat(recipes)
                .Concat(favorites)
                .Select(recipe => recipe.Id);
            ViewData["RecipeLikeSummaries"] = await RecipeLikeQueries.LoadAsync(
                _db, displayedIds, currentUserId);
            ViewData["RecipeRatingSummaries"] = await RecipeRatingQueries.LoadAsync(
                _db, displayedIds, currentUserId);
            ViewData["SavedRecipeIds"] = await FavoriteQueries.LoadSavedIdsAsync(
                _db, displayedIds, currentUserId);

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Follow(string userId)
        {
            var followerId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(followerId))
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(userId)
                || userId == followerId
                || !await _db.Users.AnyAsync(user => user.Id == userId))
            {
                return NotFound();
            }

            var existingFollow = await _db.Follows
                .FirstOrDefaultAsync(follow =>
                    follow.FollowerId == followerId
                    && follow.FollowingId == userId);

            if (existingFollow == null)
            {
                await using var transaction =
                    await _db.Database.BeginTransactionAsync();

                var follow = new Follow
                {
                    FollowerId = followerId,
                    FollowingId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Follows.Add(follow);
                await _db.SaveChangesAsync();
                await new NotificationService(_db)
                    .NotifyFollowAsync(follow);

                await transaction.CommitAsync();
            }

            return RedirectToAction(nameof(Index), new { userId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unfollow(string userId)
        {
            var followerId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(followerId))
            {
                return Challenge();
            }

            var follow = await _db.Follows.FirstOrDefaultAsync(item =>
                item.FollowerId == followerId
                && item.FollowingId == userId);

            if (follow != null)
            {
                _db.Follows.Remove(follow);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index), new { userId });
        }

        // GET: /Profile/Edit
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var model = new EditProfileViewModel
            {
                DisplayName =
                    user.DisplayName ?? user.UserName ?? "",
                AvatarUrl = user.AvatarUrl,
                CoverImageUrl = user.CoverImageUrl,
                Bio = user.Bio
            };

            return View(model);
        }

        // POST: /Profile/Edit
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            EditProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (!string.IsNullOrWhiteSpace(model.AvatarUrl))
            {
                var avatar = model.AvatarUrl.Trim();

                var isLocal = avatar.StartsWith('/')
                    && !avatar.StartsWith(
                        "//", StringComparison.Ordinal)
                    && !avatar.StartsWith(
                        "/\\", StringComparison.Ordinal)
                    && !avatar.Contains('\\');

                var isHttps = Uri.TryCreate(
                    avatar,
                    UriKind.Absolute,
                    out var uri)
                    && uri.Scheme == Uri.UriSchemeHttps
                    && string.IsNullOrEmpty(uri.UserInfo);

                if (!isLocal && !isHttps)
                {
                    ModelState.AddModelError(
                        nameof(model.AvatarUrl),
                        "Ảnh đại diện phải là đường dẫn nội bộ hoặc URL HTTPS hợp lệ.");

                    return View(model);
                }
            }

            if (!string.IsNullOrWhiteSpace(model.CoverImageUrl)
                && !RecipeImageReference.IsValid(model.CoverImageUrl))
            {
                ModelState.AddModelError(
                    nameof(model.CoverImageUrl),
                    "Ảnh bìa phải là đường dẫn nội bộ hoặc URL HTTP/HTTPS hợp lệ.");

                return View(model);
            }

            var bio = string.IsNullOrWhiteSpace(model.Bio)
                ? null
                : model.Bio.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
            if (bio != null && (bio.Contains('<', StringComparison.Ordinal) || bio.Contains('>', StringComparison.Ordinal)))
            {
                ModelState.AddModelError(
                    nameof(model.Bio),
                    "Tiểu sử chỉ nhận văn bản thường, không chứa HTML.");

                return View(model);
            }

            user.DisplayName = model.DisplayName.Trim();

            user.AvatarUrl =
                string.IsNullOrWhiteSpace(model.AvatarUrl)
                    ? null
                    : model.AvatarUrl.Trim();
            user.CoverImageUrl =
                string.IsNullOrWhiteSpace(model.CoverImageUrl)
                    ? null
                    : model.CoverImageUrl.Trim();
            user.Bio = bio;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return View(model);
            }

            TempData["Success"] =
                "Đã cập nhật thông tin cá nhân.";

            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Highlights(int[]? recipeIds)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            int[] ids = recipeIds ?? Array.Empty<int>();
            if (ids.Length > 3)
            {
                TempData["Error"] = "Chỉ được ghim tối đa 3 công thức.";
                return RedirectToAction(nameof(Index));
            }

            if (ids.Distinct().Count() != ids.Length)
            {
                TempData["Error"] = "Không thể ghim trùng một công thức.";
                return RedirectToAction(nameof(Index));
            }

            if (ids.Length > 0)
            {
                int ownedCount = await _db.Recipes.CountAsync(recipe =>
                    ids.Contains(recipe.Id) && recipe.UserId == userId);
                if (ownedCount != ids.Length)
                {
                    TempData["Error"] = "Chỉ được ghim công thức do chính bạn đăng.";
                    return RedirectToAction(nameof(Index));
                }
            }

            List<FeaturedRecipe> existing = await _db.FeaturedRecipes
                .Where(pin => pin.UserId == userId)
                .ToListAsync();
            _db.FeaturedRecipes.RemoveRange(existing);
            await _db.SaveChangesAsync();

            for (int index = 0; index < ids.Length; index++)
            {
                _db.FeaturedRecipes.Add(new FeaturedRecipe
                {
                    UserId = userId,
                    RecipeId = ids[index],
                    SortOrder = index
                });
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = ids.Length == 0
                ? "Đã bỏ hết công thức nổi bật."
                : "Đã cập nhật công thức nổi bật.";
            return RedirectToAction(nameof(Index));
        }
    }
}
