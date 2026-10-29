using CookShare.Services;
using Microsoft.AspNetCore.Mvc;

namespace CookShare.Controllers
{
    public sealed class RecipeImagesController(IRecipeImageStorage storage) : Controller
    {
        [HttpGet("/uploads/recipes/{fileName}")]
        public IActionResult Get(string fileName)
        {
            var image = storage.OpenRead(fileName);
            if (image == null)
            {
                return NotFound();
            }

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers.CacheControl = "public,max-age=31536000,immutable";
            return File(image.Content, image.ContentType, enableRangeProcessing: true);
        }
    }
}
