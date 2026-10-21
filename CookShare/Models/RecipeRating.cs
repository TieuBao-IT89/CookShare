
using CookShare.Models.Identity;

namespace CookShare.Models
{
    public class RecipeRating
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public int RecipeId { get; set; }

        public int Stars { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser User { get; set; } = null!;

        public Recipe Recipe { get; set; } = null!;
    }
}
