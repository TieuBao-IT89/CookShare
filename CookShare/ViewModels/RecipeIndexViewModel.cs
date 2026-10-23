using CookShare.Models;

namespace CookShare.ViewModels
{
    public class RecipeIndexViewModel
    {
        public IReadOnlyList<Recipe> Recipes { get; set; } = Array.Empty<Recipe>();

        public string Query { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;
    }
}
