using CookShare.Models;
using CookShare.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Data
{
    public static class RecipeRatingQueries
    {
        public static IQueryable<RecipeRatingViewModel> SelectRatingStates(
            this IQueryable<Recipe> recipes, string? viewerId)
        {
            return recipes.Select(recipe => new RecipeRatingViewModel
            {
                RecipeId = recipe.Id,
                RatingCount = recipe.Ratings.Count,
                AverageRating = recipe.Ratings.Average(rating => (double?)rating.Stars) ?? 0,
                UserRating = recipe.Ratings.Where(rating => rating.UserId == viewerId && viewerId != null)
                    .Select(rating => (int?)rating.Stars).FirstOrDefault(),
                CanRate = viewerId != null && recipe.UserId != viewerId,
                ViewerId = viewerId
            });
        }

        public static async Task<Dictionary<int, RecipeRatingViewModel>> LoadAsync(
            ApplicationDbContext db, IEnumerable<int> recipeIds, string? viewerId)
        {
            int[] ids = recipeIds.Distinct().ToArray();
            if (ids.Length == 0)
            {
                return new Dictionary<int, RecipeRatingViewModel>();
            }

            return await db.Recipes.AsNoTracking().Where(recipe => ids.Contains(recipe.Id))
                .SelectRatingStates(viewerId).ToDictionaryAsync(state => state.RecipeId);
        }
    }
}
