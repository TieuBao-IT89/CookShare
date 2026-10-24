using CookShare.Models.Identity;

namespace CookShare.Models
{
    public class FeaturedRecipe
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public int RecipeId { get; set; }

        public int SortOrder { get; set; }

        public ApplicationUser User { get; set; } = null!;

        public Recipe Recipe { get; set; } = null!;
    }
}
