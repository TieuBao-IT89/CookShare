using System.Diagnostics;
using System.Security.Claims;
using CookShare.Data;
using CookShare.Models;
using CookShare.ViewModels;
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
        public async Task<IActionResult> Index()
        {
            var featured = await RecipeDiscoveryQueries.FeaturedAsync(_db);
            var popular = await RecipeDiscoveryQueries.PopularAsync(_db);
            var topRated = await RecipeDiscoveryQueries.TopRatedAsync(_db);
            var latest = await RecipeDiscoveryQueries.LatestAsync(_db);

            var communityUsers = await _db.Users
                .AsNoTracking()
                .Where(u => u.Recipes.Any())
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
                .ToListAsync();

            var displayedIds = featured
                .Concat(popular)
                .Concat(topRated)
                .Concat(latest)
                .Select(recipe => recipe.Id);
            var currentUserId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewData["SavedRecipeIds"] = await FavoriteQueries.LoadSavedIdsAsync(
                _db, displayedIds, currentUserId);
            ViewData["RecipeLikeSummaries"] = await RecipeLikeQueries.LoadAsync(
                _db, displayedIds, currentUserId);

            return View(new HomeIndexViewModel
            {
                FeaturedRecipes = featured,
                PopularRecipes = popular,
                TopRatedRecipes = topRated,
                LatestRecipes = latest,
                CommunityUsers = communityUsers
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
