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

        public AccountDeletionService(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
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

            await _db.RecipeLikes
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync();

            await _db.RecipeComments
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync();

            await _db.Favorites
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync();

            await _db.RecipeRatings
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync();

            await _db.Follows
                .Where(x =>
                    x.FollowerId == userId ||
                    x.FollowingId == userId)
                .ExecuteDeleteAsync();

            var recipeIds = await _db.Recipes
                .Where(r => r.UserId == userId)
                .Select(r => r.Id)
                .ToListAsync();

            if (recipeIds.Count > 0)
            {
                await _db.RecipeLikes
                    .Where(x => recipeIds.Contains(x.RecipeId))
                    .ExecuteDeleteAsync();

                await _db.RecipeComments
                    .Where(x => recipeIds.Contains(x.RecipeId))
                    .ExecuteDeleteAsync();

                await _db.Favorites
                    .Where(x => recipeIds.Contains(x.RecipeId))
                    .ExecuteDeleteAsync();

                await _db.RecipeRatings
                    .Where(x => recipeIds.Contains(x.RecipeId))
                    .ExecuteDeleteAsync();

                await _db.Recipes
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
            return result;
        }
    }
}
