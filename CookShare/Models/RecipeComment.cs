
using System.ComponentModel.DataAnnotations;
using CookShare.Models.Identity;

namespace CookShare.Models
{
    public class RecipeComment
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public int RecipeId { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser User { get; set; } = null!;

        public Recipe Recipe { get; set; } = null!;
    }
}
