using System.Globalization;
using System.Text;
using CookShare.Models;
using CookShare.ViewModels;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Data
{
    public sealed record RecipeFeedCursor(DateTime CreatedAt, int Id, int Kind = 0)
    {
        public override string ToString()
        {
            return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
                FormattableString.Invariant($"2:{CreatedAt.Ticks}:{Kind}:{Id}")));
        }

        public static bool TryParse(string? value, out RecipeFeedCursor? cursor)
        {
            cursor = null;
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            if (value.Length > 128)
            {
                return false;
            }

            try
            {
                string[] parts = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)).Split(':');
                bool legacy = parts.Length == 2;
                int offset = legacy ? 0 : 1;
                if ((!legacy && (parts.Length != 4 || parts[0] != "2"))
                    || !long.TryParse(parts[offset], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks)
                    || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks
                    || !int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0)
                {
                    return false;
                }
                int kind = 0;
                if (!legacy && (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out kind) || kind is < 0 or > 1))
                {
                    return false;
                }
                cursor = new RecipeFeedCursor(new DateTime(ticks, DateTimeKind.Utc), id, kind);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }

    public static class RecipeFeedQueries
    {
        public const int PageSize = 8;

        public static IQueryable<FeedRecipeViewModel> PageQuery(
            ApplicationDbContext db, string? viewerId, bool following, RecipeFeedCursor? cursor)
        {
            IQueryable<Recipe> recipes = db.Recipes.AsNoTracking();
            if (following)
            {
                // The viewer comes from authenticated claims, never a request-supplied user ID.
                recipes = recipes.Where(recipe => viewerId != null && db.Follows.Any(follow =>
                    follow.FollowerId == viewerId && follow.FollowingId == recipe.UserId));
            }

            var recipePosts = recipes.Select(recipe => new FeedRecipeViewModel
            {
                Id = recipe.Id,
                RecipeId = recipe.Id,
                Kind = 0,
                Name = recipe.Name,
                Description = recipe.Description,
                Image = recipe.Image,
                Category = recipe.Category,
                PreparationTimeMinutes = recipe.PreparationTimeMinutes,
                CookingTimeMinutes = recipe.CookingTimeMinutes,
                Difficulty = recipe.Difficulty,
                CreatedAt = recipe.CreatedAt,
                AuthorId = recipe.UserId,
                AuthorName = recipe.User == null ? "Thành viên CookShare" : recipe.User.DisplayName ?? recipe.User.UserName ?? "Thành viên CookShare",
                AvatarUrl = recipe.User == null ? null : recipe.User.AvatarUrl,
                LikeCount = recipe.Likes.Count,
                IsLiked = viewerId != null && recipe.Likes.Any(like => like.UserId == viewerId),
                CommentCount = recipe.Comments.Count,
                RatingCount = recipe.Ratings.Count,
                AverageRating = recipe.Ratings.Average(rating => (double?)rating.Stars) ?? 0,
                IsSaved = viewerId != null && recipe.Favorites.Any(favorite => favorite.UserId == viewerId)
            });
            var cooked = db.CookedRecipePosts.AsNoTracking();
            if (following)
            {
                cooked = cooked.Where(post => viewerId != null && db.Follows.Any(follow =>
                    follow.FollowerId == viewerId && follow.FollowingId == post.UserId));
            }
            var cookedPosts = cooked.Select(post => new FeedRecipeViewModel
            {
                Id = post.Id,
                RecipeId = post.RecipeId,
                Kind = 1,
                Name = post.Recipe.Name,
                Description = post.Note,
                Image = "/media/cooked-posts/" + post.Id + "/" + post.ImageFileName,
                Category = post.Recipe.Category,
                PreparationTimeMinutes = post.Recipe.PreparationTimeMinutes,
                CookingTimeMinutes = post.Recipe.CookingTimeMinutes,
                Difficulty = post.Recipe.Difficulty,
                CreatedAt = post.CreatedAt,
                AuthorId = post.UserId,
                AuthorName = post.User.DisplayName ?? post.User.UserName ?? "Thành viên CookShare",
                AvatarUrl = post.User.AvatarUrl,
                LikeCount = 0,
                IsLiked = false,
                CommentCount = 0,
                RatingCount = 0,
                AverageRating = 0,
                IsSaved = false
            });
            var posts = recipePosts.Concat(cookedPosts);
            if (cursor != null)
            {
                posts = posts.Where(post => post.CreatedAt < cursor.CreatedAt || post.CreatedAt == cursor.CreatedAt &&
                    (post.Kind < cursor.Kind || post.Kind == cursor.Kind && post.Id < cursor.Id));
            }
            return posts.OrderByDescending(post => post.CreatedAt).ThenByDescending(post => post.Kind)
                .ThenByDescending(post => post.Id).Take(PageSize + 1);
        }

        public static async Task<RecipeFeedPage> LoadAsync(
            ApplicationDbContext db, string? viewerId, string mode, RecipeFeedCursor? cursor,
            CancellationToken cancellationToken = default)
        {
            if (mode is not ("latest" or "following"))
            {
                throw new ArgumentException("Invalid feed mode.", nameof(mode));
            }

            if (mode == "following" && string.IsNullOrEmpty(viewerId))
            {
                throw new ArgumentException("Following feed requires an authenticated viewer.", nameof(viewerId));
            }

            var rows = await PageQuery(db, viewerId, mode == "following", cursor).ToListAsync(cancellationToken);
            bool hasMore = rows.Count > PageSize;
            var selected = rows.Take(PageSize).ToList();
            return new RecipeFeedPage
            {
                Recipes = selected,
                Mode = mode,
                ViewerId = viewerId,
                NextCursor = hasMore ? new RecipeFeedCursor(selected[^1].CreatedAt, selected[^1].Id, selected[^1].Kind).ToString() : null
            };
        }
    }
}
