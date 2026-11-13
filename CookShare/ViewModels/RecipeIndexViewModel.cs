using CookShare.Models;

namespace CookShare.ViewModels
{
    public class RecipeIndexViewModel
    {
        public IReadOnlyList<Recipe> Recipes { get; set; } = Array.Empty<Recipe>();

        public string Query { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public RecipeDifficulty? Difficulty { get; set; }

        public int? MaxTotalMinutes { get; set; }

        public string Ingredient { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Sort { get; set; } = "newest";
        public string? NextCursor { get; set; }
        public int TotalCount { get; set; }
    }
}
