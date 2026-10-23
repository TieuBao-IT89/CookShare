using CookShare.Data;
using CookShare.Models;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Tests.Data
{
    public sealed class RecipeDiscoveryQueriesTests
    {
        [Fact]
        public async Task Ranking_ShouldOrderFeaturedPopularAndTopRatedAndKeepEachSectionLimited()
        {
            await using var db = CreateContext();
            var timestamp = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            var quiet = await AddRecipeAsync(db, "Quiet", timestamp);
            var likedOnce = await AddRecipeAsync(db, "Liked once", timestamp.AddHours(1));
            var likedTwiceOlder = await AddRecipeAsync(db, "Liked twice older", timestamp.AddHours(2));
            var likedTwiceNewer = await AddRecipeAsync(db, "Liked twice newer", timestamp.AddHours(3));
            var fourStars = await AddRecipeAsync(db, "Four stars", timestamp.AddHours(4));
            var fiveOnce = await AddRecipeAsync(db, "Five once", timestamp.AddHours(5));
            var fiveTwice = await AddRecipeAsync(db, "Five twice", timestamp.AddHours(6));
            var extraLiked = await AddRecipeAsync(db, "Extra liked", timestamp.AddHours(7));

            await AddLikesAsync(db, likedOnce, 1);
            await AddLikesAsync(db, likedTwiceOlder, 2);
            await AddLikesAsync(db, likedTwiceNewer, 2);
            await AddLikesAsync(db, extraLiked, 1);
            await AddRatingsAsync(db, fourStars, 4);
            await AddRatingsAsync(db, fiveOnce, 5);
            await AddRatingsAsync(db, fiveTwice, 5, 5);

            var featured = await RecipeDiscoveryQueries.FeaturedAsync(db);
            var featuredOrder = await RecipeDiscoveryQueries.FeaturedAsync(db, limit: 7);
            var popular = await RecipeDiscoveryQueries.PopularAsync(db);
            var topRated = await RecipeDiscoveryQueries.TopRatedAsync(db);
            var latest = await RecipeDiscoveryQueries.LatestAsync(db);

            Assert.Equal(3, RecipeDiscoveryQueries.SectionSize);
            Assert.Equal(new[] { fiveTwice.Id, fiveOnce.Id, likedTwiceNewer.Id }, Ids(featured));
            Assert.Equal(
                new[]
                {
                    fiveTwice.Id,
                    fiveOnce.Id,
                    likedTwiceNewer.Id,
                    likedTwiceOlder.Id,
                    fourStars.Id,
                    extraLiked.Id,
                    likedOnce.Id
                },
                Ids(featuredOrder));
            Assert.Equal(new[] { likedTwiceNewer.Id, likedTwiceOlder.Id, extraLiked.Id }, Ids(popular));
            Assert.Equal(new[] { fiveTwice.Id, fiveOnce.Id, fourStars.Id }, Ids(topRated));
            Assert.Equal(new[] { extraLiked.Id, fiveTwice.Id, fiveOnce.Id }, Ids(latest));
            Assert.DoesNotContain(featured, recipe => recipe.Id == quiet.Id);
            Assert.DoesNotContain(popular, recipe => recipe.Id == quiet.Id);
            Assert.DoesNotContain(topRated, recipe => recipe.Id == quiet.Id);
        }

        [Fact]
        public async Task Related_ShouldStayInCategoryExcludeCurrentAndPreferLikes()
        {
            await using var db = CreateContext();
            var timestamp = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            var current = await AddRecipeAsync(db, "Pho", timestamp, "Món Việt");
            var liked = await AddRecipeAsync(db, "Bun", timestamp.AddHours(1), "món việt");
            var rated = await AddRecipeAsync(db, "Rolls", timestamp.AddHours(2), "MÓN VIỆT");
            var plainOlder = await AddRecipeAsync(db, "Older", timestamp, "Món Việt");
            var plainNewer = await AddRecipeAsync(db, "Newer", timestamp.AddHours(3), "Món Việt");
            var overflow = await AddRecipeAsync(db, "Overflow", timestamp.AddHours(4), "Món Việt");
            var other = await AddRecipeAsync(db, "Cake", timestamp.AddHours(5), "Món tráng miệng");
            var blank = await AddRecipeAsync(db, "Untitled", timestamp, "   ");
            await AddLikesAsync(db, liked, 2);
            await AddRatingsAsync(db, rated, 5);

            var related = await RecipeDiscoveryQueries.RelatedAsync(db, current.Id, current.Category);
            var none = await RecipeDiscoveryQueries.RelatedAsync(db, blank.Id, blank.Category);
            var missing = await RecipeDiscoveryQueries.RelatedAsync(db, current.Id, null);

            Assert.Equal(new[] { liked.Id, rated.Id, overflow.Id }, Ids(related));
            Assert.DoesNotContain(related, recipe => recipe.Id == current.Id);
            Assert.DoesNotContain(related, recipe => recipe.Id == other.Id);
            Assert.DoesNotContain(related, recipe => recipe.Id == plainOlder.Id || recipe.Id == plainNewer.Id);
            Assert.Empty(none);
            Assert.Empty(missing);
        }

        [Fact]
        public async Task Sections_ShouldBeEmptyWhenRecipesHaveNoLikesOrRatings()
        {
            await using var db = CreateContext();
            await AddRecipeAsync(db, "Only recipe", DateTime.UtcNow);

            Assert.Empty(await RecipeDiscoveryQueries.FeaturedAsync(db));
            Assert.Empty(await RecipeDiscoveryQueries.PopularAsync(db));
            Assert.Empty(await RecipeDiscoveryQueries.TopRatedAsync(db));
            Assert.Single(await RecipeDiscoveryQueries.LatestAsync(db));
        }

        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new ApplicationDbContext(options);
            db.Database.EnsureCreated();
            return db;
        }

        private static async Task<Recipe> AddRecipeAsync(
            ApplicationDbContext db,
            string name,
            DateTime createdAt,
            string category = "Món Việt")
        {
            var recipe = new Recipe
            {
                Name = name,
                Description = name,
                Category = category,
                Ingredients = "Ingredient",
                Instructions = "Step",
                CreatedAt = createdAt
            };
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            return recipe;
        }

        private static async Task AddLikesAsync(ApplicationDbContext db, Recipe recipe, int count)
        {
            for (var index = 0; index < count; index++)
            {
                db.RecipeLikes.Add(new RecipeLike
                {
                    RecipeId = recipe.Id,
                    UserId = recipe.Id + "-fan-" + index
                });
            }

            await db.SaveChangesAsync();
        }

        private static async Task AddRatingsAsync(ApplicationDbContext db, Recipe recipe, params int[] stars)
        {
            for (var index = 0; index < stars.Length; index++)
            {
                db.RecipeRatings.Add(new RecipeRating
                {
                    RecipeId = recipe.Id,
                    UserId = recipe.Id + "-rater-" + index,
                    Stars = stars[index]
                });
            }

            await db.SaveChangesAsync();
        }

        private static int[] Ids(IEnumerable<Recipe> recipes)
        {
            return recipes.Select(recipe => recipe.Id).ToArray();
        }
    }
}
