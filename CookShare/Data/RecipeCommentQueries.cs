using CookShare.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Data
{
    public static class RecipeCommentQueries
    {
        public static Task<List<RecipeCommentViewModel>> LoadAsync(
            ApplicationDbContext db, int recipeId, string? currentUserId)
        {
            return ForRecipe(db, recipeId, currentUserId)
                .OrderByDescending(comment => comment.CreatedAt)
                .ThenByDescending(comment => comment.Id)
                .ToListAsync();
        }

        public static IQueryable<RecipeCommentViewModel> ForRecipe(
            ApplicationDbContext db, int recipeId, string? currentUserId)
        {
            return db.RecipeComments
                .AsNoTracking()
                .Where(comment => comment.RecipeId == recipeId)
                .Select(comment => new RecipeCommentViewModel
                {
                    Id = comment.Id,
                    RecipeId = comment.RecipeId,
                    UserId = comment.UserId,
                    AuthorName = !string.IsNullOrWhiteSpace(comment.User.DisplayName)
                        ? comment.User.DisplayName
                        : comment.User.UserName ?? "Thành viên CookShare",
                    AvatarUrl = comment.User.AvatarUrl,
                    Content = comment.Content,
                    CreatedAt = comment.CreatedAt,
                    CanManage = currentUserId != null && comment.UserId == currentUserId
                });
        }
    }
}
