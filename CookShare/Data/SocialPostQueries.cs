using CookShare.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Data
{
    public static class SocialPostQueries
    {
        public static IQueryable<CookedPostViewModel> Cards(ApplicationDbContext db, string? viewerId)
            => db.CookedRecipePosts.AsNoTracking().Select(post => new CookedPostViewModel
            {
                Id = post.Id,
                RecipeId = post.RecipeId,
                RecipeName = post.Recipe.Name,
                AuthorId = post.UserId,
                AuthorName = post.User.DisplayName ?? post.User.UserName ?? "Thành viên CookShare",
                AvatarUrl = post.User.AvatarUrl,
                Note = post.Note,
                ImageUrl = "/media/cooked-posts/" + post.Id + "/" + post.ImageFileName,
                CreatedAt = post.CreatedAt,
                IsOwner = post.UserId == viewerId
            });
    }
}
