using System.Data;
using CookShare.Data;
using CookShare.Helpers;
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Tests.Data
{
    public sealed class SocialDiscoveryTests
    {
        [Theory]
        [InlineData("ĐẬU HŨ", "", "")]
        [InlineData("dau hu", "", "")]
        [InlineData("", "đậu nành", "")]
        [InlineData("", "dau nanh", "")]
        [InlineData("", "", "Bếp Đỏ")]
        [InlineData("", "", "bep do")]
        [InlineData("dau hu", "nam", "bep do")]
        public async Task Search_ShouldNormalizeVietnameseAccentsAndDStrokeAcrossPublicFields(string query, string ingredient, string author)
        {
            await using var db = CreateDb();
            SeedUsers(db);
            db.Recipes.AddRange(Recipe(1, "Đậu hũ sốt nấm", "chef", "Đậu nành\nNấm hương"),
                Recipe(2, "Cơm chiên", "other", "Gạo\nTrứng"));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var page = await SocialDiscoveryQueries.LoadAsync(db, Options(query, ingredient, author), null);

            Assert.Equal(1, Assert.Single(page.Recipes).Id);
            Assert.Equal(1, page.TotalCount);
            Assert.Equal("dau hu sot nam", VietnameseSearch.Normalize("Đậu hũ sốt nấm"));
            Assert.Empty(db.ChangeTracker.Entries());
        }

        [Theory]
        [InlineData("%", 1)]
        [InlineData("_", 2)]
        [InlineData("' OR 1=1 --", 3)]
        public async Task SpecialSearchCharacters_ShouldMatchLiteralTextOnly(string query, int expectedId)
        {
            await using var db = CreateDb();
            SeedUsers(db);
            db.Recipes.AddRange(Recipe(1, "Món 100% vị nhà", "chef"), Recipe(2, "Cơm_nhà", "chef"),
                Recipe(3, "' OR 1=1 --", "chef"), Recipe(4, "Món bình thường", "chef"));
            await db.SaveChangesAsync();

            var page = await SocialDiscoveryQueries.LoadAsync(db, Options(query), null);

            Assert.Equal(expectedId, Assert.Single(page.Recipes).Id);
        }

        [Fact]
        public async Task EmptySearchAndFilters_ShouldHandleLegacyNullTimingAndHideModeratedRecipes()
        {
            await using var db = CreateDb();
            SeedUsers(db);
            var matched = Recipe(1, "Món nhanh", "chef");
            matched.PreparationTimeMinutes = 5;
            matched.CookingTimeMinutes = 20;
            matched.Difficulty = RecipeDifficulty.Easy;
            var slow = Recipe(2, "Món chậm", "chef");
            slow.PreparationTimeMinutes = 20;
            slow.CookingTimeMinutes = 40;
            var legacy = Recipe(3, "Món cũ", "chef");
            var hidden = Recipe(4, "Món ẩn", "chef");
            hidden.IsHidden = true;
            db.Recipes.AddRange(matched, slow, legacy, hidden);
            await db.SaveChangesAsync();

            var all = await SocialDiscoveryQueries.LoadAsync(db, Options(), null);
            Assert.Equal(3, all.TotalCount);
            Assert.DoesNotContain(all.Recipes, recipe => recipe.Id == hidden.Id);
            var filtered = await SocialDiscoveryQueries.LoadAsync(db,
                Options() with { MaxTotalMinutes = 30, Difficulty = RecipeDifficulty.Easy, Category = "Món Việt" }, null);
            Assert.Equal(matched.Id, Assert.Single(filtered.Recipes).Id);
        }

        [Fact]
        public async Task CursorPaging_ShouldRemainBoundedOrderedAndExcludeNewInsertionsAfterFirstPage()
        {
            await using var db = CreateDb();
            SeedUsers(db);
            DateTime date = DateTime.UtcNow.AddDays(-1);
            for (int id = 1; id <= 73; id++) db.Recipes.Add(Recipe(id, "Món " + id, "chef", date: date));
            await db.SaveChangesAsync();
            var options = Options();
            var page = await SocialDiscoveryQueries.LoadAsync(db, options, null);
            Assert.Equal(SocialDiscoveryQueries.PageSize, page.Recipes.Count);
            Assert.Equal(73, page.TotalCount);
            var seen = page.Recipes.Select(recipe => recipe.Id).ToList();
            db.Recipes.Add(Recipe(1000, "Món vừa đăng", "chef", date: DateTime.UtcNow.AddMinutes(1)));
            await db.SaveChangesAsync();
            while (page.NextCursor != null)
            {
                Assert.True(DiscoveryCursor.TryParse(page.NextCursor, options, out var cursor));
                Assert.NotNull(cursor);
                page = await SocialDiscoveryQueries.LoadAsync(db, options, cursor);
                Assert.InRange(page.Recipes.Count, 1, SocialDiscoveryQueries.PageSize);
                Assert.Equal(73, page.TotalCount);
                seen.AddRange(page.Recipes.Select(recipe => recipe.Id));
            }
            Assert.Equal(Enumerable.Range(1, 73).Reverse(), seen);
            Assert.Equal(seen.Count, seen.Distinct().Count());
        }

        [Fact]
        public async Task PopularSort_ShouldUseWeightedInteractionScoreThenTimeThenId()
        {
            await using var db = CreateDb();
            SeedUsers(db);
            DateTime date = DateTime.UtcNow.AddDays(-1);
            db.Recipes.AddRange(Recipe(1, "Một lượt thích", "chef", date: date),
                Recipe(2, "Hai bình luận", "chef", date: date),
                Recipe(3, "Năm sao", "chef", date: date.AddDays(-1)), Recipe(4, "Mới nhất chưa tương tác", "chef", date: date.AddHours(1)));
            db.RecipeLikes.Add(new RecipeLike { RecipeId = 1, UserId = "reader" });
            db.RecipeComments.AddRange(new RecipeComment { RecipeId = 2, UserId = "reader", Content = "Một" },
                new RecipeComment { RecipeId = 2, UserId = "other", Content = "Hai" });
            db.RecipeRatings.Add(new RecipeRating { RecipeId = 3, UserId = "reader", Stars = 5 });
            await db.SaveChangesAsync();

            var page = await SocialDiscoveryQueries.LoadAsync(db, Options() with { Sort = "popular" }, null);

            Assert.Equal(new[] { 3, 2, 1, 4 }, page.Recipes.Select(recipe => recipe.Id));
        }

        [Fact]
        public async Task SearchProjection_ShouldReturnOnlyPublicIdentityFields()
        {
            await using var db = CreateDb();
            SeedUsers(db);
            db.Recipes.Add(Recipe(1, "Món thật", "chef"));
            await db.SaveChangesAsync();

            var card = Assert.Single((await SocialDiscoveryQueries.LoadAsync(db, Options(), null)).Recipes);

            Assert.Equal("Bếp Đỏ", card.User?.DisplayName);
            Assert.Null(card.User?.Email);
            Assert.Null(card.User?.PhoneNumber);
            Assert.Null(card.User?.PasswordHash);
            // IdentityUser creates a fresh stamp in its constructor; the persisted
            // authentication stamp must never be projected onto a public card.
            Assert.NotEqual("private-stamp", card.User?.SecurityStamp);
        }

        [Fact]
        public void Cursor_ShouldRejectChangedFiltersAndMalformedTokens()
        {
            var options = Options("đậu", "nấm", "bếp đỏ");
            DateTime date = DateTime.UtcNow.AddDays(-1);
            var original = new DiscoveryCursor(date, 12, 7, date.AddHours(1));
            string token = original.Encode(options);
            Assert.True(DiscoveryCursor.TryParse(token, options, out var decoded));
            Assert.Equal(original, decoded);
            Assert.False(DiscoveryCursor.TryParse(token, options with { Sort = "popular" }, out _));
            Assert.False(DiscoveryCursor.TryParse(token, options with { Ingredient = "gạo" }, out _));
            Assert.False(DiscoveryCursor.TryParse("not-a-cursor?", options, out _));
            Assert.False(DiscoveryCursor.TryParse(new string('x', 201), options, out _));
            Assert.True(DiscoveryCursor.TryParse(null, options, out var empty));
            Assert.Null(empty);
        }

        [Fact]
        public void SearchSql_ShouldRemainParameterizedAndAvoidPrivateIdentityColumns()
        {
            using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=cookshare_test_discovery;Username=query_only").Options);

            string sql = SocialDiscoveryQueries.Filter(db.Recipes.AsNoTracking(), Options("100%", "rau_", "bếp đỏ"))
                .OrderByDescending(recipe => recipe.CreatedAt).ThenByDescending(recipe => recipe.Id)
                .Take(SocialDiscoveryQueries.PageSize + 1).Select(recipe => new { recipe.Id, recipe.Name, recipe.User!.DisplayName }).ToQueryString();

            Assert.Contains("LIMIT", sql);
            Assert.Contains("SearchIngredients", sql);
            Assert.Contains("PublicSearchName", sql);
            Assert.DoesNotContain("PasswordHash", sql);
            Assert.DoesNotContain("SecurityStamp", sql);
            Assert.DoesNotContain("\"Email\"", sql);
            Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
        }

        private static DiscoveryOptions Options(string query = "", string ingredient = "", string author = "") => new(query, ingredient, author, "", null, null, "newest");

        private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static void SeedUsers(ApplicationDbContext db)
        {
            db.Users.AddRange(new ApplicationUser { Id = "chef", UserName = "chef", DisplayName = "Bếp Đỏ", Email = "private@example.test", PasswordHash = "private-password", SecurityStamp = "private-stamp", PhoneNumber = "private-phone" },
                new ApplicationUser { Id = "other", UserName = "other", DisplayName = "Bếp Xanh" },
                new ApplicationUser { Id = "reader", UserName = "reader" });
        }

        private static Recipe Recipe(int id, string name, string owner, string ingredients = "Gạo", DateTime? date = null) => new()
        {
            Id = id,
            Name = name,
            UserId = owner,
            Description = "Món nhà",
            Category = "Món Việt",
            Ingredients = ingredients,
            Instructions = "Nấu",
            CreatedAt = date ?? DateTime.UtcNow.AddDays(-1)
        };
    }
}
