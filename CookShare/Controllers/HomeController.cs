using System.Diagnostics;
using System.Security.Claims;
using CookShare.Data;
using CookShare.Models;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HomeController(ApplicationDbContext db)
        {
            _db = db;
        }

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index(string feed = "latest", string? cursor = null, CancellationToken cancellationToken = default)
        {
            if (feed is not ("latest" or "following") || !RecipeFeedCursor.TryParse(cursor, out var position))
            {
                return BadRequest();
            }

            string? viewerId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
            if (feed == "following" && string.IsNullOrEmpty(viewerId))
            {
                return Challenge();
            }

            var page = await RecipeFeedQueries.LoadAsync(_db, viewerId, feed, position, cancellationToken);
            var profile = viewerId == null ? null : await _db.Users.AsNoTracking()
                .Where(user => user.Id == viewerId)
                .Select(user => new FeedProfileViewModel
                {
                    Id = user.Id,
                    Name = user.DisplayName ?? user.UserName ?? "Bạn",
                    AvatarUrl = user.AvatarUrl,
                    RecipeCount = user.Recipes.Count,
                    FollowingCount = _db.Follows.Count(follow => follow.FollowerId == user.Id)
                }).SingleOrDefaultAsync(cancellationToken);
            var featured = await RecipeDiscoveryQueries.FeaturedAsync(_db);
            var popular = await RecipeDiscoveryQueries.PopularAsync(_db);
            var topRated = await RecipeDiscoveryQueries.TopRatedAsync(_db);

            var communityUsers = await _db.Users
                .AsNoTracking()
                .Where(u => u.Recipes.Count > 0 && u.Id != viewerId
                    && !_db.Follows.Any(follow => follow.FollowerId == viewerId && follow.FollowingId == u.Id))
                .OrderBy(u => u.CreatedAt)
                .ThenBy(u => u.Id)
                .Take(3)
                .Select(u => new ProfileUserItemViewModel
                {
                    UserId = u.Id,
                    UserName = u.UserName ?? "",
                    DisplayName = u.DisplayName ?? u.UserName ?? "CookShare User",
                    AvatarUrl = u.AvatarUrl
                })
                .ToListAsync(cancellationToken);

            return View("Index", new HomeIndexViewModel
            {
                Feed = page,
                CurrentProfile = profile,
                FeaturedRecipes = featured,
                PopularRecipes = popular,
                TopRatedRecipes = topRated,
                CommunityUsers = communityUsers
            });
        }

        [Authorize]
        [HttpGet("/Feed/Following")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public Task<IActionResult> Following(string? cursor = null, CancellationToken cancellationToken = default)
        {
            return Index("following", cursor, cancellationToken);
        }

        [HttpGet("/Feed/More")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> More(string feed = "latest", string? cursor = null, CancellationToken cancellationToken = default)
        {
            if (feed is not ("latest" or "following") || !RecipeFeedCursor.TryParse(cursor, out var position))
            {
                return BadRequest();
            }

            string? viewerId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
            if (feed == "following" && string.IsNullOrEmpty(viewerId))
            {
                return Unauthorized();
            }

            var page = await RecipeFeedQueries.LoadAsync(_db, viewerId, feed, position, cancellationToken);
            return PartialView("_FeedBatch", page);
        }

        [HttpGet("/Feed/Comments/{recipeId:int}")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Comments(int recipeId, CancellationToken cancellationToken = default)
        {
            if (!await _db.Recipes.AnyAsync(recipe => recipe.Id == recipeId, cancellationToken))
            {
                return NotFound();
            }

            string? viewerId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
            var comments = await RecipeCommentQueries.ForRecipe(_db, recipeId, viewerId)
                .OrderByDescending(comment => comment.CreatedAt).ThenByDescending(comment => comment.Id)
                .Take(5).ToListAsync(cancellationToken);
            ViewData["CommentsHeadingId"] = $"feedCommentsHeading_{recipeId}";
            ViewData["FeedViewerId"] = viewerId;
            ViewData["InteractionReturnUrl"] = Url.Action("Index", "Home");
            return PartialView("_RecipeComments", new RecipeCommentsViewModel
            {
                RecipeId = recipeId,
                Comments = comments,
                TotalCount = await _db.RecipeComments.CountAsync(comment => comment.RecipeId == recipeId, cancellationToken),
                IsAuthenticated = viewerId != null
            });
        }

        public IActionResult Username()
        {
            return View();
        }

        public IActionResult Login()
        {
            return RedirectToAction("Login", "Account");
        }

        public IActionResult Register()
        {
            return RedirectToAction("Register", "Account");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
