using CookShare.Data;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Services
{
    public class AccountDeletionService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISocialImageStorage? _socialImages;
        private readonly IRecipeImageStorage? _recipeImages;

        public AccountDeletionService(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ISocialImageStorage? socialImages = null, IRecipeImageStorage? recipeImages = null)
        {
            _db = db;
            _userManager = userManager;
            _socialImages = socialImages;
            _recipeImages = recipeImages;
        }

        // Công thức, tương tác và quan hệ theo dõi đang Restrict
        // tới người dùng. Xóa các dòng đó trước, rồi mới xóa tài khoản,
        // để không để lại khóa ngoại mồ côi.
        public async Task<IdentityResult> DeleteAsync(
            ApplicationUser user)
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync();

            var userId = user.Id;

            var ownedRecipeIds = await _db.Recipes.IgnoreQueryFilters().Where(recipe => recipe.UserId == userId)
                .Select(recipe => recipe.Id).ToListAsync();
            var socialFiles = await _db.CookedRecipePosts.IgnoreQueryFilters()
                .Where(post => post.UserId == userId || ownedRecipeIds.Contains(post.RecipeId))
                .Select(post => post.ImageFileName).ToListAsync();
            var recipeFiles = await _db.Recipes.IgnoreQueryFilters().Where(recipe => recipe.UserId == userId && recipe.Image != null)
                .Select(recipe => recipe.Image!).ToListAsync();
            await _db.RecipeCollections.Where(collection => collection.OwnerId == userId).ExecuteDeleteAsync();
            await _db.CookedRecipePosts.IgnoreQueryFilters()
                .Where(post => post.UserId == userId || ownedRecipeIds.Contains(post.RecipeId)).ExecuteDeleteAsync();

            // Creator Profile was added after the original account settings PR.
            // Remove this user's featured slots explicitly; recipe-linked slots
            // are also removed below before deleting the owned recipes.
            await _db.ProfileFeaturedRecipes.IgnoreQueryFilters()
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync();

            await _db.RecipeLikes.IgnoreQueryFilters()
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync();

            await _db.RecipeComments.IgnoreQueryFilters()
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync();

            await _db.Favorites.IgnoreQueryFilters()
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync();

            await _db.RecipeRatings.IgnoreQueryFilters()
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync();

            await _db.Follows
                .Where(x =>
                    x.FollowerId == userId ||
                    x.FollowingId == userId)
                .ExecuteDeleteAsync();

            await _db.Notifications
                .Where(x =>
                    x.ActorId == userId ||
                    x.RecipientId == userId)
                .ExecuteDeleteAsync();

            var recipeIds = await _db.Recipes.IgnoreQueryFilters()
                .Where(r => r.UserId == userId)
                .Select(r => r.Id)
                .ToListAsync();

            if (recipeIds.Count > 0)
            {
                await _db.ProfileFeaturedRecipes.IgnoreQueryFilters()
                    .Where(x => recipeIds.Contains(x.RecipeId))
                    .ExecuteDeleteAsync();

                await _db.Notifications
                    .Where(x =>
                        x.RecipeId != null &&
                        recipeIds.Contains(x.RecipeId.Value))
                    .ExecuteDeleteAsync();

                await _db.RecipeLikes.IgnoreQueryFilters()
                    .Where(x => recipeIds.Contains(x.RecipeId))
                    .ExecuteDeleteAsync();

                await _db.RecipeComments.IgnoreQueryFilters()
                    .Where(x => recipeIds.Contains(x.RecipeId))
                    .ExecuteDeleteAsync();

                await _db.Favorites.IgnoreQueryFilters()
                    .Where(x => recipeIds.Contains(x.RecipeId))
                    .ExecuteDeleteAsync();

                await _db.RecipeRatings.IgnoreQueryFilters()
                    .Where(x => recipeIds.Contains(x.RecipeId))
                    .ExecuteDeleteAsync();

                await _db.Recipes.IgnoreQueryFilters()
                    .Where(r => r.UserId == userId)
                    .ExecuteDeleteAsync();
            }

            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                return result;
            }

            await transaction.CommitAsync();
            foreach (string file in socialFiles)
            {
                try { if (_socialImages != null) await _socialImages.DeleteAsync(file); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { /* Orphan cleanup can retry after commit. */ }
            }
            foreach (string file in recipeFiles)
            {
                if (await _db.Recipes.IgnoreQueryFilters().AnyAsync(recipe => recipe.Image == file)) continue;
                try { if (_recipeImages != null) await _recipeImages.DeleteAsync(file); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { /* Preserve the completed deletion. */ }
            }
            return result;
        }
    }
}
