using System.ComponentModel.DataAnnotations;
using CookShare.Models.Identity;

namespace CookShare.Models
{
    public sealed class CookedRecipePost
    {
        public int Id { get; set; }
        public int RecipeId { get; set; }
        [Required]
        public string UserId { get; set; } = string.Empty;
        [MaxLength(1000)]
        public string Note { get; set; } = string.Empty;
        [Required, MaxLength(40)]
        public string ImageFileName { get; set; } = string.Empty;
        public Guid SubmissionKey { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsHidden { get; set; }
        public Recipe Recipe { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
    }
}
