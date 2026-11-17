using CookShare.Data;
using CookShare.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Controllers
{
    public sealed class RecipeImagesController(IRecipeImageStorage storage, ApplicationDbContext db) : Controller
    {
        [HttpGet("/uploads/recipes/{fileName}")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Get(string fileName)
        {
            string reference = "/uploads/recipes/" + fileName;
            if (!await db.Recipes.AnyAsync(recipe => recipe.Image == reference)
                || await db.Recipes.IgnoreQueryFilters().AnyAsync(recipe => recipe.Image == reference && recipe.IsHidden))
            {
                return NotFound();
            }
            var image = storage.OpenRead(fileName);
            if (image == null)
            {
                return NotFound();
            }

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers.CacheControl = "no-store";
            return File(image.Content, image.ContentType, enableRangeProcessing: true);
        }
    }
}
