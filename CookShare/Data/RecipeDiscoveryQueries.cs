using CookShare.Models;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Data
{
    public static class RecipeDiscoveryQueries
    {
        public const int SectionSize = 3;

        /// <summary>
        /// Featured score is (like count × 2) + the sum of star ratings.
        /// A like is worth two points and each star is worth one, so both
        /// popularity and ratings can surface a recipe. Recipes with no likes
        /// and no ratings are left out. Ties prefer more likes, then newer recipes.
        /// </summary>
        public static Task<List<Recipe>> FeaturedAsync(
            ApplicationDbContext db,
            int limit = SectionSize)
        {
            var ranked = db.Recipes.AsNoTracking()
                .Select(recipe => new RankRow
                {
                    Id = recipe.Id,
                    LikeCount = recipe.Likes.Count(),
                    RatingCount = recipe.Ratings.Count(),
                    RatingSum = recipe.Ratings.Sum(rating => (int?)rating.Stars) ?? 0,
                    CreatedAt = recipe.CreatedAt
                })
                .Where(row => row.LikeCount > 0 || row.RatingCount > 0)
                .OrderByDescending(row => (row.LikeCount * 2) + row.RatingSum)
                .ThenByDescending(row => row.LikeCount)
                .ThenByDescending(row => row.CreatedAt)
                .ThenByDescending(row => row.Id)
                .Take(limit)
                .Select(row => row.Id);

            return LoadInRankOrderAsync(db, ranked);
        }

        /// <summary>
        /// Popular recipes are ordered by like count. A recipe with no likes
        /// is not popular yet, so the section stays empty until the first like.
        /// </summary>
        public static Task<List<Recipe>> PopularAsync(
            ApplicationDbContext db,
            int limit = SectionSize)
        {
            var ranked = db.Recipes.AsNoTracking()
                .Select(recipe => new RankRow
                {
                    Id = recipe.Id,
                    LikeCount = recipe.Likes.Count(),
                    CreatedAt = recipe.CreatedAt
                })
                .Where(row => row.LikeCount > 0)
                .OrderByDescending(row => row.LikeCount)
                .ThenByDescending(row => row.CreatedAt)
                .ThenByDescending(row => row.Id)
                .Take(limit)
                .Select(row => row.Id);

            return LoadInRankOrderAsync(db, ranked);
        }

        /// <summary>
        /// Top rated recipes need at least one rating. Order is average stars,
        /// then the number of ratings, so a single five-star vote does not
        /// outrank the same average from more people. Unrated recipes stay out.
        /// </summary>
        public static Task<List<Recipe>> TopRatedAsync(
            ApplicationDbContext db,
            int limit = SectionSize)
        {
            var ranked = db.Recipes.AsNoTracking()
                .Select(recipe => new RankRow
                {
                    Id = recipe.Id,
                    RatingCount = recipe.Ratings.Count(),
                    RatingSum = recipe.Ratings.Sum(rating => (int?)rating.Stars) ?? 0,
                    CreatedAt = recipe.CreatedAt
                })
                .Where(row => row.RatingCount > 0)
                .OrderByDescending(row => (double)row.RatingSum / row.RatingCount)
                .ThenByDescending(row => row.RatingCount)
                .ThenByDescending(row => row.CreatedAt)
                .ThenByDescending(row => row.Id)
                .Take(limit)
                .Select(row => row.Id);

            return LoadInRankOrderAsync(db, ranked);
        }

        public static Task<List<Recipe>> LatestAsync(
            ApplicationDbContext db,
            int limit = SectionSize)
        {
            var ranked = db.Recipes.AsNoTracking()
                .OrderByDescending(recipe => recipe.CreatedAt)
                .ThenByDescending(recipe => recipe.Id)
                .Take(limit)
                .Select(recipe => recipe.Id);

            return LoadInRankOrderAsync(db, ranked);
        }

        /// <summary>
        /// Related recipes share the current category and never include the
        /// recipe being viewed. Blank categories have no relatives. Ranking
        /// prefers likes, then the rating total, then newer recipes.
        /// </summary>
        public static Task<List<Recipe>> RelatedAsync(
            ApplicationDbContext db,
            int recipeId,
            string? category,
            int limit = SectionSize)
        {
            // Parameterless ToLower translates to PostgreSQL lower(); the culture and
            // StringComparison overloads cannot be translated by this EF provider.
#pragma warning disable CA1304, CA1311, CA1862
            var key = category?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(key))
            {
                return Task.FromResult(new List<Recipe>());
            }

            var ranked = db.Recipes.AsNoTracking()
                .Where(recipe => recipe.Id != recipeId && recipe.Category.ToLower() == key)
                .Select(recipe => new RankRow
                {
                    Id = recipe.Id,
                    LikeCount = recipe.Likes.Count(),
                    RatingSum = recipe.Ratings.Sum(rating => (int?)rating.Stars) ?? 0,
                    CreatedAt = recipe.CreatedAt
                })
                .OrderByDescending(row => row.LikeCount)
                .ThenByDescending(row => row.RatingSum)
                .ThenByDescending(row => row.CreatedAt)
                .ThenByDescending(row => row.Id)
                .Take(limit)
                .Select(row => row.Id);
#pragma warning restore CA1304, CA1311, CA1862

            return LoadInRankOrderAsync(db, ranked);
        }

        private static async Task<List<Recipe>> LoadInRankOrderAsync(
            ApplicationDbContext db,
            IQueryable<int> rankedIds)
        {
            var ids = await rankedIds.ToListAsync();
            if (ids.Count == 0)
            {
                return new List<Recipe>();
            }

            var recipes = await db.Recipes
                .AsNoTracking()
                .Include(recipe => recipe.User)
                .Where(recipe => ids.Contains(recipe.Id))
                .ToListAsync();

            var order = new Dictionary<int, int>(ids.Count);
            for (var index = 0; index < ids.Count; index++)
            {
                order[ids[index]] = index;
            }

            recipes.Sort((left, right) => order[left.Id].CompareTo(order[right.Id]));
            return recipes;
        }

        private sealed class RankRow
        {
            public int Id { get; set; }

            public int LikeCount { get; set; }

            public int RatingCount { get; set; }

            public int RatingSum { get; set; }

            public DateTime CreatedAt { get; set; }
        }
    }
}
