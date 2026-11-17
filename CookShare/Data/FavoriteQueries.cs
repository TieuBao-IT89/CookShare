using Microsoft.EntityFrameworkCore;

namespace CookShare.Data
{
    public static class FavoriteQueries
    {
        public static async Task<HashSet<int>> LoadSavedIdsAsync(
            ApplicationDbContext db, IEnumerable<int> recipeIds, string? userId)
        {
            int[] ids = recipeIds.Distinct().ToArray();
            if (string.IsNullOrEmpty(userId) || ids.Length == 0)
            {
                return new HashSet<int>();
            }

            return (await db.Favorites.AsNoTracking()
                .Where(favorite => favorite.UserId == userId && ids.Contains(favorite.RecipeId))
                .Select(favorite => favorite.RecipeId)
                .ToListAsync()).ToHashSet();
        }
    }
}
