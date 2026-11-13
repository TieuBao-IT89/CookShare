using CookShare.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CookShare.Tests.Integration
{
    [Collection("PostgreSQL integration")]
    public sealed class SocialMigrationUpgradePostgresTests
    {
        [DisposableFavoritesPostgresFact]
        public async Task Upgrade_ShouldPreserveAllLegacyRowsAndBackfillVietnameseSearch()
        {
            string rootConnection = Environment.GetEnvironmentVariable("COOKSHARE_TEST_CONNECTION")!;
            var target = new NpgsqlConnectionStringBuilder(rootConnection)
            {
                Database = "cookshare_test_social_upgrade_" + Guid.NewGuid().ToString("N")
            };
            bool created = false;
            await using var admin = new NpgsqlConnection(rootConnection);
            await admin.OpenAsync();
            try
            {
                await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{target.Database}\"", admin))
                {
                    await create.ExecuteNonQueryAsync();
                    created = true;
                }
                await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseNpgsql(target.ConnectionString).Options);
                await db.GetService<IMigrator>().MigrateAsync("20261030142753_AddRecipeCookingMetadata");
                await using var sql = new NpgsqlConnection(target.ConnectionString);
                await sql.OpenAsync();
                await using (var seed = new NpgsqlCommand("""
                    INSERT INTO "AspNetUsers" ("Id", "UserName", "DisplayName", "CreatedAt", "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
                    VALUES ('old-a', 'legacy-a', 'Đặng – Bếp cũ', NOW(), FALSE, FALSE, FALSE, TRUE, 0),
                           ('old-b', 'legacy-b', 'Lan', NOW(), FALSE, FALSE, FALSE, TRUE, 0);
                    INSERT INTO "Recipes" ("Id", "Name", "Description", "Ingredients", "Instructions", "Category", "UserId", "CreatedAt", "Servings")
                    VALUES (1, 'Phở đậu Hà Nội', 'Giữ nguyên mô tả', '200 g đậu hũ', 'Nấu 20 phút', 'Món Việt', 'old-a', NOW(), 4);
                    INSERT INTO "Favorites" ("UserId", "RecipeId", "CreatedAt") VALUES ('old-b', 1, NOW());
                    INSERT INTO "RecipeLikes" ("UserId", "RecipeId", "CreatedAt") VALUES ('old-b', 1, NOW());
                    INSERT INTO "RecipeComments" ("UserId", "RecipeId", "Content", "CreatedAt") VALUES ('old-b', 1, 'Món ngon từ bếp cũ', NOW());
                    INSERT INTO "RecipeRatings" ("UserId", "RecipeId", "Stars", "CreatedAt") VALUES ('old-b', 1, 5, NOW());
                    INSERT INTO "Follows" ("FollowerId", "FollowingId", "CreatedAt") VALUES ('old-b', 'old-a', NOW());
                    INSERT INTO "Notifications" ("RecipientId", "ActorId", "Type", "Message", "RecipeId", "EventKey", "IsRead", "CreatedAt")
                    VALUES ('old-a', 'old-b', 'Like', 'Thông báo có trước Social 2.0', 1, 'old-notification:1', FALSE, NOW());
                    INSERT INTO "ProfileFeaturedRecipes" ("UserId", "RecipeId", "DisplayOrder") VALUES ('old-a', 1, 1);
                    """, sql))
                {
                    await seed.ExecuteNonQueryAsync();
                }
                var before = await SnapshotAsync(sql);
                await db.Database.MigrateAsync();
                var after = await SnapshotAsync(sql);
                foreach (var table in before.Keys)
                {
                    Assert.Equal(before[table], after[table]);
                }
                var recipe = await db.Recipes.SingleAsync();
                Assert.False(recipe.IsHidden);
                Assert.Contains("pho dau", recipe.SearchText, StringComparison.Ordinal);
                Assert.Contains("dau hu", recipe.SearchIngredients, StringComparison.Ordinal);
                Assert.StartsWith("dang", (await db.Users.SingleAsync(user => user.Id == "old-a")).PublicSearchName, StringComparison.Ordinal);
                Assert.Empty(await db.RecipeCollections.ToListAsync());
                Assert.Empty(await db.CookedRecipePosts.ToListAsync());
                Assert.Empty(await db.ContentReports.ToListAsync());
                await using var indexes = new NpgsqlCommand("""
                    SELECT count(*) FROM pg_indexes WHERE schemaname = 'public'
                      AND indexname IN ('IX_Recipes_SearchText', 'IX_Recipes_SearchIngredients', 'IX_AspNetUsers_PublicSearchName')
                      AND indexdef LIKE '%USING gin%'
                    """, sql);
                Assert.Equal(3L, await indexes.ExecuteScalarAsync());
            }
            finally
            {
                if (created)
                {
                    // Only this test's unique disposable database is terminated/dropped.
                    await using var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid()", admin);
                    terminate.Parameters.AddWithValue("name", target.Database!);
                    await terminate.ExecuteNonQueryAsync();
                    await using var drop = new NpgsqlCommand($"DROP DATABASE \"{target.Database}\"", admin);
                    await drop.ExecuteNonQueryAsync();
                }
            }
        }

        private static async Task<Dictionary<string, string>> SnapshotAsync(NpgsqlConnection sql)
        {
            var snapshots = new Dictionary<string, string>();
            string[] tables = ["AspNetUsers", "Recipes", "Favorites", "RecipeLikes", "RecipeComments", "RecipeRatings", "Follows", "Notifications", "ProfileFeaturedRecipes"];
            foreach (string table in tables)
            {
                await using var command = new NpgsqlCommand($"""
                    SELECT COALESCE(jsonb_agg(legacy ORDER BY legacy::text)::text, '[]')
                    FROM (SELECT to_jsonb(legacy_row) - 'SearchText' - 'SearchIngredients' - 'PublicSearchName' - 'IsHidden' - 'CookedRecipePostId' AS legacy FROM "{table}" legacy_row) records
                    """, sql);
                snapshots[table] = (string)(await command.ExecuteScalarAsync())!;
            }
            return snapshots;
        }
    }
}
