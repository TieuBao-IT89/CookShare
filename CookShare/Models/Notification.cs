using System.ComponentModel.DataAnnotations;
using CookShare.Models.Identity;

namespace CookShare.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        public string RecipientId { get; set; } = string.Empty;

        [Required]
        public string ActorId { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Message { get; set; }

        public int? RecipeId { get; set; }

        public int? CommentId { get; set; }

        [Required]
        [MaxLength(200)]
        public string EventKey { get; set; } = string.Empty;

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser Recipient { get; set; } = null!;

        public ApplicationUser Actor { get; set; } = null!;
    }
}