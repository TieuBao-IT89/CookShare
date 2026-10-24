
using CookShare.Models;
using Microsoft.AspNetCore.Identity;

namespace CookShare.Models.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string? DisplayName { get; set; }

        public string? AvatarUrl { get; set; }

        public string? CoverImageUrl { get; set; }

        public string? Bio { get; set; }

        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;

        public ICollection<Recipe> Recipes { get; set; }
            = new List<Recipe>();

        public ICollection<FeaturedRecipe> FeaturedRecipes { get; set; }
            = new List<FeaturedRecipe>();
    }
}
