using CookShare.Data;
using CookShare.Models;
using CookShare.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Controllers
{
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public sealed class CookedPostImagesController(ApplicationDbContext db, ISocialImageStorage storage) : Controller
    {
        [HttpGet("/media/cooked-posts/{id:int}/{fileName}")]
        public async Task<IActionResult> Get(int id, string fileName, CancellationToken cancellationToken = default)
        {
            if (!await db.Set<CookedRecipePost>().AsNoTracking().AnyAsync(post => post.Id == id
                && post.ImageFileName == fileName && !post.IsHidden && !post.Recipe.IsHidden, cancellationToken))
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
            return File(image.Content, image.ContentType, enableRangeProcessing: false);
        }
    }
}
