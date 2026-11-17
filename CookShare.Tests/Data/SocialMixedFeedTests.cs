using System.Data;
using System.Globalization;
using System.Text;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Tests.Data
{
    public sealed class SocialMixedFeedTests
    {
        [Fact]
        public async Task MixedCursor_ShouldIncludeBothKindsWithMatchingTimestampAndIdExactlyOnce()
        {
            await using var db = CreateDb();
            DateTime date = await SeedMixedAsync(db);
            var expected = Enumerable.Range(1, 12).Reverse().Select(id => (Kind: 1, Id: id))
                .Concat(Enumerable.Range(1, 12).Reverse().Select(id => (Kind: 0, Id: id))).ToArray();
            var page = await RecipeFeedQueries.LoadAsync(db, "reader", "latest", null);
            var actual = page.Recipes.Select(item => (item.Kind, item.Id)).ToList();
            Assert.Equal(RecipeFeedQueries.PageSize, page.Recipes.Count);
            Assert.All(page.Recipes, post => { Assert.Equal(1, post.Kind); Assert.Equal(12, post.RecipeId); });
            while (page.NextCursor != null)
            {
                Assert.True(RecipeFeedCursor.TryParse(page.NextCursor, out var cursor));
                Assert.Equal(date, cursor!.CreatedAt);
                page = await RecipeFeedQueries.LoadAsync(db, "reader", "latest", cursor);
                Assert.InRange(page.Recipes.Count, 1, RecipeFeedQueries.PageSize);
                actual.AddRange(page.Recipes.Select(item => (item.Kind, item.Id)));
            }
            Assert.Equal(expected, actual);
            Assert.Equal(24, actual.Distinct().Count());
            Assert.Empty(db.ChangeTracker.Entries());
        }

        [Fact]
        public async Task NewRowsInEitherTable_ShouldNotRepeatOrDisplaceExistingRowsAfterCursor()
        {
            await using var db = CreateDb();
            await SeedMixedAsync(db);
            var page = await RecipeFeedQueries.LoadAsync(db, null, "latest", null);
            var actual = page.Recipes.Select(item => (item.Kind, item.Id)).ToList();
            db.Recipes.Add(Recipe(1000, "New recipe", "chef", DateTime.UtcNow));
            db.CookedRecipePosts.Add(Post(1000, 12, "chef", DateTime.UtcNow));
            await db.SaveChangesAsync();
            while (page.NextCursor != null)
            {
                Assert.True(RecipeFeedCursor.TryParse(page.NextCursor, out var cursor));
                page = await RecipeFeedQueries.LoadAsync(db, null, "latest", cursor);
                actual.AddRange(page.Recipes.Select(item => (item.Kind, item.Id)));
            }
            Assert.Equal(24, actual.Count);
            Assert.Equal(24, actual.Distinct().Count());
            Assert.DoesNotContain(actual, item => item.Id == 1000);
        }

        [Fact]
        public async Task Following_ShouldUsePostAuthorAndDropBothKindsAfterUnfollow()
        {
            await using var db = CreateDb();
            DateTime date = await SeedMixedAsync(db);
            db.Recipes.Add(Recipe(100, "Other author's recipe", "other", date));
            // Following the recipe author must not also follow every person who cooked it.
            db.CookedRecipePosts.Add(Post(100, 12, "other", date));
            await db.SaveChangesAsync();
            var page = await RecipeFeedQueries.LoadAsync(db, "reader", "following", null);
            Assert.NotEmpty(page.Recipes);
            Assert.All(page.Recipes, item => Assert.Equal("chef", item.AuthorId));
            var query = await RecipeFeedQueries.PageQuery(db, "reader", true, null).ToListAsync();
            Assert.All(query, item => Assert.NotEqual(100, item.Id));
            db.Follows.Remove(await db.Follows.SingleAsync());
            await db.SaveChangesAsync();

            Assert.True(RecipeFeedCursor.TryParse(page.NextCursor, out var cursor));
            Assert.Empty((await RecipeFeedQueries.LoadAsync(db, "reader", "following", cursor)).Recipes);
            Assert.Empty((await RecipeFeedQueries.LoadAsync(db, "reader", "following", null)).Recipes);
            Assert.Empty(await RecipeFeedQueries.PageQuery(db, null, true, null).ToListAsync());
        }

        [Fact]
        public async Task Visibility_ShouldRequireBothCookedPostAndParentRecipeToRemainPublic()
        {
            await using var db = CreateDb();
            SeedUsers(db);
            DateTime date = DateTime.UtcNow.AddDays(-1);
            var hidden = Recipe(2, "Hidden parent", "chef", date);
            hidden.IsHidden = true;
            db.Recipes.AddRange(Recipe(1, "Visible parent", "chef", date), hidden);
            var hiddenPost = Post(2, 1, "chef", date);
            hiddenPost.IsHidden = true;
            db.CookedRecipePosts.AddRange(Post(1, 1, "chef", date), hiddenPost, Post(3, 2, "chef", date));
            await db.SaveChangesAsync();

            var page = await RecipeFeedQueries.LoadAsync(db, null, "latest", null);

            Assert.Equal(new[] { (Kind: 1, Id: 1), (Kind: 0, Id: 1) }, page.Recipes.Select(item => (item.Kind, item.Id)));
        }

        [Fact]
        public void MixedQuery_ShouldTranslateToOneBoundedUnionWithoutPrivateIdentityColumns()
        {
            using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=cookshare_test_mixed_feed;Username=query_only").Options);

            string sql = RecipeFeedQueries.PageQuery(db, "reader", true,
                new RecipeFeedCursor(DateTime.UtcNow.AddDays(-1), 7, 1)).ToQueryString();

            Assert.Contains("UNION ALL", sql);
            Assert.Contains("CookedRecipePosts", sql);
            Assert.Contains("LIMIT", sql);
            Assert.DoesNotContain("OFFSET", sql);
            Assert.DoesNotContain("PasswordHash", sql);
            Assert.DoesNotContain("SecurityStamp", sql);
            Assert.DoesNotContain("\"Email\"", sql);
            Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void MixedCursor_ShouldRoundTripKindAndReadLegacyRecipeCursor(int kind)
        {
            DateTime date = DateTime.UtcNow.AddDays(-1);
            var original = new RecipeFeedCursor(date, 23, kind);
            Assert.True(RecipeFeedCursor.TryParse(original.ToString(), out var cursor));
            Assert.Equal(original, cursor);
            string legacy = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(date.Ticks.ToString(CultureInfo.InvariantCulture) + ":23"));
            Assert.True(RecipeFeedCursor.TryParse(legacy, out var oldCursor));
            Assert.Equal(new RecipeFeedCursor(date, 23, 0), oldCursor);
            string invalidKind = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("2:" + date.Ticks.ToString(CultureInfo.InvariantCulture) + ":9:23"));
            Assert.False(RecipeFeedCursor.TryParse(invalidKind, out _));
        }

        private static async Task<DateTime> SeedMixedAsync(ApplicationDbContext db)
        {
            SeedUsers(db);
            DateTime date = DateTime.UtcNow.AddDays(-1);
            for (int id = 1; id <= 12; id++)
            {
                db.Recipes.Add(Recipe(id, "Recipe " + id, "chef", date));
                db.CookedRecipePosts.Add(Post(id, 12, "chef", date));
            }
            db.Follows.Add(new Follow { FollowerId = "reader", FollowingId = "chef" });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            return date;
        }

        private static void SeedUsers(ApplicationDbContext db) => db.Users.AddRange(
            new ApplicationUser { Id = "reader", UserName = "reader" },
            new ApplicationUser { Id = "chef", UserName = "chef", DisplayName = "Bếp Đỏ" },
            new ApplicationUser { Id = "other", UserName = "other" });

        private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static Recipe Recipe(int id, string name, string owner, DateTime date) => new()
        {
            Id = id,
            Name = name,
            UserId = owner,
            CreatedAt = date,
            Category = "Món Việt",
            Description = "Món nhà",
            Ingredients = "Rau",
            Instructions = "Nấu"
        };

        private static CookedRecipePost Post(int id, int recipeId, string owner, DateTime date) => new()
        {
            Id = id,
            RecipeId = recipeId,
            UserId = owner,
            CreatedAt = date,
            Note = "Thành phẩm",
            ImageFileName = Guid.NewGuid().ToString("N") + ".png",
            SubmissionKey = Guid.NewGuid()
        };
    }
}
