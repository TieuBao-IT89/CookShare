using System.Globalization;
using System.Text;
using CookShare.Models;
using CookShare.ViewModels;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Data
{
    public sealed record RecipeFeedCursor(DateTime CreatedAt, int Id)
    {
        public override string ToString()
        {
            return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
                FormattableString.Invariant($"{CreatedAt.Ticks}:{Id}")));
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
                if (parts.Length != 2
                    || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks)
                    || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks
                    || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0)
                {
                    return false;
                }

                cursor = new RecipeFeedCursor(new DateTime(ticks, DateTimeKind.Utc), id);
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

            if (cursor != null)
            {
                recipes = recipes.Where(recipe => recipe.CreatedAt < cursor.CreatedAt
                    || (recipe.CreatedAt == cursor.CreatedAt && recipe.Id < cursor.Id));
            }

            // Project public fields and aggregate state in one bounded query; no Identity secrets or N+1 loads.
            return recipes.OrderByDescending(recipe => recipe.CreatedAt).ThenByDescending(recipe => recipe.Id)
                .Take(PageSize + 1)
                .Select(recipe => new FeedRecipeViewModel
                {
                    Id = recipe.Id,
                    Name = recipe.Name,
                    Description = recipe.Description,
                    Image = recipe.Image,
                    Category = recipe.Category,
                    CreatedAt = recipe.CreatedAt,
                    AuthorId = recipe.UserId,
                    AuthorName = recipe.User == null ? "Thành viên CookShare"
                        : recipe.User.DisplayName ?? recipe.User.UserName ?? "Thành viên CookShare",
                    AvatarUrl = recipe.User == null ? null : recipe.User.AvatarUrl,
                    LikeCount = recipe.Likes.Count,
                    IsLiked = viewerId != null && recipe.Likes.Any(like => like.UserId == viewerId),
                    CommentCount = recipe.Comments.Count,
                    IsSaved = viewerId != null && recipe.Favorites.Any(favorite => favorite.UserId == viewerId)
                });
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
                NextCursor = hasMore ? new RecipeFeedCursor(selected[^1].CreatedAt, selected[^1].Id).ToString() : null
            };
        }
    }
}
