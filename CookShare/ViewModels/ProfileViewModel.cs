
using CookShare.Models;

namespace CookShare.ViewModels
{
    public class ProfileViewModel
    {
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public string? CoverImageUrl { get; set; }

        public string? Bio { get; set; }

        public DateTime JoinedAt { get; set; }

        public int RecipeCount { get; set; }

        public int FollowersCount { get; set; }

        public int FollowingCount { get; set; }

        public bool IsOwner { get; set; }

        public bool IsFollowing { get; set; }

        public List<Recipe> FeaturedRecipes { get; set; } = new();

        public List<Recipe> Recipes { get; set; } = new();

        public List<ProfileUserItemViewModel> Followers { get; set; } = new();

        public List<ProfileUserItemViewModel> Following { get; set; } = new();

        public List<Recipe> Favorites { get; set; } = new();
    }
}
