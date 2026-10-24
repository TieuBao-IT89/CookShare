using CookShare.Models.Identity;

namespace CookShare.Models
{
    public sealed class ProfileFeaturedRecipe
    {
        public string UserId { get; set; } = string.Empty;
        public int RecipeId { get; set; }
        public int DisplayOrder { get; set; }
        public ApplicationUser User { get; set; } = null!;
        public Recipe Recipe { get; set; } = null!;
    }
}
