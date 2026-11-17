using CookShare.Models;
using CookShare.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Data
{
    public static class RecipeLikeQueries
    {
        public static IQueryable<RecipeLikeViewModel> SelectLikeStates(
            this IQueryable<Recipe> recipes, string? currentUserId)
        {
            return recipes.Select(recipe => new RecipeLikeViewModel
            {
                RecipeId = recipe.Id,
                LikeCount = recipe.Likes.Count,
                IsLiked = currentUserId != null && recipe.Likes.Any(like => like.UserId == currentUserId)
            });
        }

        public static async Task<Dictionary<int, RecipeLikeViewModel>> LoadAsync(
            ApplicationDbContext db, IEnumerable<int> recipeIds, string? currentUserId)
        {
            int[] ids = recipeIds.Distinct().ToArray();
            if (ids.Length == 0)
            {
                return new Dictionary<int, RecipeLikeViewModel>();
            }

            return await db.Recipes
                .AsNoTracking()
                .Where(recipe => ids.Contains(recipe.Id))
                .SelectLikeStates(currentUserId)
                .ToDictionaryAsync(state => state.RecipeId);
        }
    }
}
