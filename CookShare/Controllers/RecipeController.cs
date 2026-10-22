using System.Security.Claims;
using CookShare.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CookShare.Controllers
{
    public class RecipeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public IActionResult Create()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Edit(
            int id,
            [FromServices] ApplicationDbContext db)
        {
            var recipe = await db.Recipes.FindAsync(id);

            if (recipe == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(currentUserId) || recipe.UserId != currentUserId)
            {
                return Forbid();
            }

            ViewBag.RecipeId = id;
            return View();
        }

        public IActionResult Details(int id)
        {
            ViewBag.RecipeId = id;
            return View();
        }

        [Authorize]
        public IActionResult Saved()
        {
            return View();
        }
    }
}
