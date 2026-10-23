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
            var latest = await _db.Recipes
                .AsNoTracking()
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Take(3)
                .ToListAsync();

            // A stable selection until a recommendation feature is available.
            var featured = await _db.Recipes
                .AsNoTracking()
                .Include(r => r.User)
                .OrderBy(r => r.Id)
                .Take(3)
                .ToListAsync();

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

            ViewData["RecipeLikeSummaries"] = await RecipeLikeQueries.LoadAsync(
                _db, latest.Concat(featured).Select(recipe => recipe.Id),
                User.FindFirstValue(ClaimTypes.NameIdentifier));
            ViewData["SavedRecipeIds"] = await FavoriteQueries.LoadSavedIdsAsync(
                _db, latest.Concat(featured).Select(recipe => recipe.Id),
                User.FindFirstValue(ClaimTypes.NameIdentifier));

            return View(new HomeIndexViewModel
            {
                LatestRecipes = latest,
                FeaturedRecipes = featured,
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
