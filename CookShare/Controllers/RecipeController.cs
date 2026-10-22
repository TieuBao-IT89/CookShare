
using System.Security.Claims;
using CookShare.Data;
using CookShare.Models;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Controllers
{
    public class RecipeController : Controller
    {
        private readonly ApplicationDbContext _db;

        public RecipeController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET: /Recipe
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var recipes = await _db.Recipes
                .AsNoTracking()
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(recipes);
        }

        // GET: /Recipe/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var recipe = await _db.Recipes
                .AsNoTracking()
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
            {
                return NotFound();
            }

            return View(recipe);
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

            var recipe = new Recipe
            {
                Name = model.Name.Trim(),
                Description = model.Description.Trim(),
                Ingredients = model.Ingredients.Trim(),
                Instructions = model.Instructions.Trim(),
                Category = model.Category.Trim(),
                Image = model.Image,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _db.Recipes.Add(recipe);
            await _db.SaveChangesAsync();

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
                Category = recipe.Category,
                Image = recipe.Image
            };

            return View(model);
        }

        // POST: /Recipe/Edit/5
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
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

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            recipe.Name = model.Name.Trim();
            recipe.Description = model.Description.Trim();
            recipe.Ingredients = model.Ingredients.Trim();
            recipe.Instructions = model.Instructions.Trim();
            recipe.Category = model.Category.Trim();
            recipe.Image = model.Image;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật công thức.";

            return RedirectToAction(
                nameof(Details),
                new { id = recipe.Id });
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

            _db.Recipes.Remove(recipe);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã xóa công thức.";

            return RedirectToAction(nameof(Index));
        }

        // Chức năng yêu thích sẽ hoàn thiện ở Issue tiếp theo
        [Authorize]
        [HttpGet]
        public IActionResult Saved()
        {
            return View();
        }
    }
}
