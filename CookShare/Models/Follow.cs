
using CookShare.Models.Identity;

namespace CookShare.Models
{
    public class Follow
    {
        public int Id { get; set; }

        public string FollowerId { get; set; } = string.Empty;

        public string FollowingId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser Follower { get; set; } = null!;

        public ApplicationUser Following { get; set; } = null!;
    }
}

