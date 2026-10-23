using CookShare.Models;

namespace CookShare.ViewModels
{
    public class HomeIndexViewModel
    {
        public IReadOnlyList<Recipe> FeaturedRecipes { get; set; } = Array.Empty<Recipe>();

        public IReadOnlyList<Recipe> PopularRecipes { get; set; } = Array.Empty<Recipe>();

        public IReadOnlyList<Recipe> TopRatedRecipes { get; set; } = Array.Empty<Recipe>();

        public IReadOnlyList<Recipe> LatestRecipes { get; set; } = Array.Empty<Recipe>();

        public IReadOnlyList<ProfileUserItemViewModel> CommunityUsers { get; set; } = Array.Empty<ProfileUserItemViewModel>();
    }
}
