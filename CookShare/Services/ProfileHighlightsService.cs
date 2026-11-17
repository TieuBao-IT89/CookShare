using CookShare.Data;
using CookShare.Models;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Services
{
    public sealed class ProfileHighlightsService(ApplicationDbContext db)
    {
        public async Task<bool> ReplaceAsync(string userId, IReadOnlyList<int> recipeIds, CancellationToken cancellationToken = default)
        {
            if (recipeIds.Count > 3 || recipeIds.Any(id => id <= 0) || recipeIds.Distinct().Count() != recipeIds.Count)
            {
                return false;
            }

            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            if (db.Database.IsNpgsql())
            {
                // Serialize only this creator's changes; other profiles remain independent.
                var lockedUser = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE")
                    .AsNoTracking().Select(user => user.Id).SingleOrDefaultAsync(cancellationToken);
                if (lockedUser == null) return false;
            }

            int[] requested = recipeIds.ToArray();
            int owned = await db.Recipes.CountAsync(recipe => recipe.UserId == userId && requested.Contains(recipe.Id), cancellationToken);
            if (owned != recipeIds.Count) return false;

            var previous = await db.ProfileFeaturedRecipes.Where(pin => pin.UserId == userId).ToListAsync(cancellationToken);
            db.ProfileFeaturedRecipes.RemoveRange(previous);
            // Delete and insert in one transaction so swapping unique slots cannot leave a partial list.
            await db.SaveChangesAsync(cancellationToken);
            db.ProfileFeaturedRecipes.AddRange(recipeIds.Select((recipeId, index) => new ProfileFeaturedRecipe
            {
                UserId = userId,
                RecipeId = recipeId,
                DisplayOrder = index + 1
            }));
            await db.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return true;
        }
    }
}
