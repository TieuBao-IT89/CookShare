using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CookShare.Helpers;
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Data
{
    public sealed record DiscoveryOptions(string Query, string Ingredient, string Author, string Category,
        RecipeDifficulty? Difficulty, int? MaxTotalMinutes, string Sort)
    {
        public string Fingerprint => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("\n", Query, Ingredient, Author, Category, Difficulty, MaxTotalMinutes, Sort))))[..16];
    }

    public sealed record DiscoveryCursor(DateTime CreatedAt, int Id, int Score, DateTime Cutoff)
    {
        public string Encode(DiscoveryOptions options) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
            FormattableString.Invariant($"1:{CreatedAt.Ticks}:{Id}:{Score}:{Cutoff.Ticks}:{options.Fingerprint}")));

        public static bool TryParse(string? value, DiscoveryOptions options, out DiscoveryCursor? cursor)
        {
            cursor = null;
            if (string.IsNullOrEmpty(value)) return true;
            if (value.Length > 200) return false;
            try
            {
                string[] parts = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)).Split(':');
                if (parts.Length != 6 || parts[0] != "1" || parts[5] != options.Fingerprint
                    || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks)
                    || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0
                    || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int score) || score < 0
                    || !long.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out long cutoff)
                    || ticks < 0 || cutoff < ticks || cutoff > DateTime.MaxValue.Ticks) return false;
                cursor = new DiscoveryCursor(new DateTime(ticks, DateTimeKind.Utc), id, score, new DateTime(cutoff, DateTimeKind.Utc));
                return true;
            }
            catch (FormatException) { return false; }
        }
    }

    public sealed record DiscoveryPage(IReadOnlyList<Recipe> Recipes, int TotalCount, string? NextCursor);

    public static class SocialDiscoveryQueries
    {
        public const int PageSize = 24;

        public static async Task<DiscoveryPage> LoadAsync(ApplicationDbContext db, DiscoveryOptions options,
            DiscoveryCursor? cursor, CancellationToken cancellationToken = default)
        {
            DateTime cutoff = cursor?.Cutoff ?? DateTime.UtcNow;
            var recipes = Filter(db.Recipes.AsNoTracking(), options).Where(recipe => recipe.CreatedAt <= cutoff);
            int total = await recipes.CountAsync(cancellationToken);
            var ranked = recipes.Select(recipe => new DiscoveryRank
            {
                Id = recipe.Id,
                CreatedAt = recipe.CreatedAt,
                Score = recipe.Likes.Count * 2 + recipe.Comments.Count + (recipe.Ratings.Sum(rating => (int?)rating.Stars) ?? 0)
            });
            bool popular = options.Sort == "popular";
            if (cursor != null)
            {
                ranked = popular
                    ? ranked.Where(row => row.Score < cursor.Score || row.Score == cursor.Score &&
                        (row.CreatedAt < cursor.CreatedAt || row.CreatedAt == cursor.CreatedAt && row.Id < cursor.Id))
                    : ranked.Where(row => row.CreatedAt < cursor.CreatedAt || row.CreatedAt == cursor.CreatedAt && row.Id < cursor.Id);
            }
            var rows = await (popular
                ? ranked.OrderByDescending(row => row.Score).ThenByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id)
                : ranked.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id))
                .Take(PageSize + 1).ToListAsync(cancellationToken);
            var selected = rows.Take(PageSize).ToList();
            int[] ids = selected.Select(row => row.Id).ToArray();
            var cards = await db.Recipes.AsNoTracking().Where(recipe => ids.Contains(recipe.Id))
                .Select(recipe => new Recipe
                {
                    Id = recipe.Id,
                    Name = recipe.Name,
                    Description = recipe.Description,
                    Image = recipe.Image,
                    Source = recipe.Source,
                    ExternalId = recipe.ExternalId,
                    SourceUrl = recipe.SourceUrl,
                    Category = recipe.Category,
                    Ingredients = recipe.Ingredients,
                    Servings = recipe.Servings,
                    PreparationTimeMinutes = recipe.PreparationTimeMinutes,
                    CookingTimeMinutes = recipe.CookingTimeMinutes,
                    Difficulty = recipe.Difficulty,
                    CreatedAt = recipe.CreatedAt,
                    UserId = recipe.UserId,
                    User = recipe.User == null ? null : new ApplicationUser
                    {
                        Id = recipe.User.Id,
                        UserName = recipe.User.UserName,
                        DisplayName = recipe.User.DisplayName,
                        AvatarUrl = recipe.User.AvatarUrl
                    }
                }).ToListAsync(cancellationToken);
            var positions = selected.Select((row, position) => (row.Id, position)).ToDictionary(row => row.Id, row => row.position);
            cards.Sort((left, right) => positions[left.Id].CompareTo(positions[right.Id]));
            return new DiscoveryPage(cards, total, rows.Count > PageSize
                ? new DiscoveryCursor(selected[^1].CreatedAt, selected[^1].Id, selected[^1].Score, cutoff).Encode(options) : null);
        }

        public static IQueryable<Recipe> Filter(IQueryable<Recipe> recipes, DiscoveryOptions options)
        {
            string query = VietnameseSearch.Normalize(options.Query);
            string ingredient = VietnameseSearch.Normalize(options.Ingredient);
            string author = VietnameseSearch.Normalize(options.Author);
            if (query.Length > 0) recipes = recipes.Where(recipe => recipe.SearchText.Contains(query));
            if (ingredient.Length > 0) recipes = recipes.Where(recipe => recipe.SearchIngredients.Contains(ingredient));
            if (author.Length > 0) recipes = recipes.Where(recipe => recipe.User != null && recipe.User.PublicSearchName.Contains(author));
            if (options.Category.Length > 0) recipes = recipes.FilterBySearchAndCategory(string.Empty, options.Category);
            if (options.Difficulty.HasValue) recipes = recipes.Where(recipe => recipe.Difficulty == options.Difficulty);
            if (options.MaxTotalMinutes.HasValue) recipes = recipes.Where(recipe => recipe.PreparationTimeMinutes.HasValue
                && recipe.CookingTimeMinutes.HasValue && recipe.PreparationTimeMinutes + recipe.CookingTimeMinutes <= options.MaxTotalMinutes);
            return recipes;
        }

        private sealed class DiscoveryRank
        {
            public int Id { get; set; }
            public DateTime CreatedAt { get; set; }
            public int Score { get; set; }
        }
    }
}
