using System.Data;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Tests.Data
{
    public sealed class RecipeFeedQueriesTests
    {
        [Fact]
        public async Task Following_ShouldIncludeOnlyFollowedAuthorsAndProjectInteractionState()
        {
            await using var db = CreateDb();
            await PrepareAsync(db);
            var first = db.Recipes.Local.Single(recipe => recipe.Name == "Recipe 17");
            db.RecipeLikes.Add(new RecipeLike { RecipeId = first.Id, UserId = "reader" });
            db.Favorites.Add(new Favorite { RecipeId = first.Id, UserId = "reader" });
            db.RecipeComments.Add(new RecipeComment { RecipeId = first.Id, UserId = "reader", Content = "Nice recipe" });
            await db.SaveChangesAsync();

            var page = await RecipeFeedQueries.LoadAsync(db, "reader", "following", null);

            Assert.Equal(8, page.Recipes.Count);
            Assert.All(page.Recipes, recipe => Assert.Equal("chef", recipe.AuthorId));
            Assert.Equal("reader", page.ViewerId);
            Assert.NotNull(page.NextCursor);
            Assert.Empty(await RecipeFeedQueries.PageQuery(db, null, true, null).ToListAsync());
            var interacted = await RecipeFeedQueries.PageQuery(db, "reader", false, null)
                .SingleAsync(recipe => recipe.Id == first.Id);
            Assert.True(interacted.IsLiked);
            Assert.True(interacted.IsSaved);
            Assert.Equal(1, interacted.LikeCount);
            Assert.Equal(1, interacted.CommentCount);
        }

        [Fact]
        public async Task CursorPagination_ShouldNotRepeatOrSkipRowsWhenNewRecipesAreInserted()
        {
            await using var db = CreateDb();
            await PrepareAsync(db);
            int[] expected = await db.Recipes.OrderByDescending(recipe => recipe.CreatedAt).ThenByDescending(recipe => recipe.Id)
                .Select(recipe => recipe.Id).ToArrayAsync();
            var page = await RecipeFeedQueries.LoadAsync(db, null, "latest", null);
            var actual = page.Recipes.Select(recipe => recipe.Id).ToList();
            db.Recipes.Add(Recipe("Inserted after first page", "chef", DateTime.UtcNow));
            await db.SaveChangesAsync();
            while (page.NextCursor != null)
            {
                Assert.True(RecipeFeedCursor.TryParse(page.NextCursor, out var cursor));
                page = await RecipeFeedQueries.LoadAsync(db, null, "latest", cursor);
                actual.AddRange(page.Recipes.Select(recipe => recipe.Id));
            }

            Assert.Equal(expected, actual);
            Assert.Equal(actual.Count, actual.Distinct().Count());
        }

        [Fact]
        public async Task Unfollow_ShouldAffectTheNextDatabaseQueryWithoutBrowserStorage()
        {
            await using var db = CreateDb();
            await PrepareAsync(db);
            Assert.NotEmpty((await RecipeFeedQueries.LoadAsync(db, "reader", "following", null)).Recipes);
            db.Follows.Remove(await db.Follows.SingleAsync());
            await db.SaveChangesAsync();

            Assert.Empty((await RecipeFeedQueries.LoadAsync(db, "reader", "following", null)).Recipes);
            await Assert.ThrowsAsync<ArgumentException>(() => RecipeFeedQueries.LoadAsync(db, null, "following", null));
        }

        [Fact]
        public void Query_ShouldTranslateToBoundedPostgresSqlWithoutLoadingPrivateIdentityFields()
        {
            using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=cookshare_test_feed;Username=feed_test").Options);
            var cursor = new RecipeFeedCursor(new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc), 42);

            string sql = RecipeFeedQueries.PageQuery(db, "reader", true, cursor).ToQueryString();

            Assert.Contains("EXISTS", sql);
            Assert.Contains("Follows", sql);
            Assert.Contains("LIMIT", sql);
            Assert.DoesNotContain("OFFSET", sql);
            Assert.DoesNotContain("PasswordHash", sql);
            Assert.DoesNotContain("SecurityStamp", sql);
            Assert.DoesNotContain("Email", sql);
            Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
        }

        [Fact]
        public void Cursor_ShouldRoundTripTimestampAndTieBreakingId()
        {
            var original = new RecipeFeedCursor(new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc), 123);
            Assert.True(RecipeFeedCursor.TryParse(original.ToString(), out var decoded));
            Assert.Equal(original, decoded);
            Assert.True(RecipeFeedCursor.TryParse(null, out var empty));
            Assert.Null(empty);
        }

        [Theory]
        [InlineData("invalid?")]
        [InlineData("LTE6MQ")]
        [InlineData("MTIzOjA")]
        [InlineData("MTIzOi0x")]
        public void Cursor_ShouldRejectMalformedInput(string value)
        {
            Assert.False(RecipeFeedCursor.TryParse(value, out _));
            Assert.False(RecipeFeedCursor.TryParse(new string('a', 129), out _));
        }

        private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("feed-query-" + Guid.NewGuid()).Options);

        private static async Task PrepareAsync(ApplicationDbContext db)
        {
            db.Users.AddRange(new ApplicationUser { Id = "reader", UserName = "reader" },
                new ApplicationUser { Id = "chef", UserName = "chef" },
                new ApplicationUser { Id = "other", UserName = "other" });
            db.Follows.Add(new Follow { FollowerId = "reader", FollowingId = "chef" });
            DateTime date = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
            for (int index = 0; index < 22; index++)
            {
                db.Recipes.Add(Recipe($"Recipe {index}", index < 18 ? "chef" : index < 20 ? "other" : "reader", date));
            }

            await db.SaveChangesAsync();
        }

        private static Recipe Recipe(string name, string owner, DateTime date) => new()
        {
            Name = name,
            UserId = owner,
            CreatedAt = date,
            Category = "Món Việt",
            Description = "A recipe from the kitchen",
            Ingredients = "Rice",
            Instructions = "Cook the rice"
        };
    }
}
