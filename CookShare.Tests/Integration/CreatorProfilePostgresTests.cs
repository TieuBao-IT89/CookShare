using System.Data.Common;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CookShare.Tests.Integration
{
    [Collection(PostgreSqlIntegrationCollection.Name)]
    public sealed class CreatorProfilePostgresTests
    {
        [PostgresFact]
        public async Task Highlights_ShouldSerializeConcurrentReordersRollbackFailuresAndCascadeDeletedRecipes()
        {
            string connection = TestConnection();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
            string author = "creator-test-" + Guid.NewGuid().ToString("N");
            int[] recipes = Array.Empty<int>();
            try
            {
                await using (var seed = new ApplicationDbContext(options))
                {
                    seed.Users.Add(new ApplicationUser { Id = author, UserName = author, Bio = "Bếp Việt\nMỗi món ngon là một câu chuyện", CoverImageUrl = "/img/recipes/pho.webp" });
                    var rows = Enumerable.Range(1, 4).Select(index => new Recipe { UserId = author, Name = "Creator recipe " + index, Description = "Recipe story", Category = "Món Việt", Ingredients = "Rice", Instructions = "Cook" }).ToArray();
                    seed.Recipes.AddRange(rows); await seed.SaveChangesAsync(); recipes = rows.Select(recipe => recipe.Id).ToArray();
                }

                var gate = new LockGate();
                var concurrent = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).AddInterceptors(gate).Options;
                int[] firstOrder = { recipes[0], recipes[1], recipes[2] };
                int[] secondOrder = { recipes[3], recipes[2], recipes[0] };
                await using (var first = new ApplicationDbContext(concurrent))
                await using (var second = new ApplicationDbContext(concurrent))
                {
                    bool[] saved = await Task.WhenAll(new ProfileHighlightsService(first).ReplaceAsync(author, firstOrder), new ProfileHighlightsService(second).ReplaceAsync(author, secondOrder));
                    Assert.All(saved, Assert.True);
                }

                int[] final;
                await using (var verify = new ApplicationDbContext(options))
                {
                    final = await PinsAsync(verify, author);
                    Assert.True(final.SequenceEqual(firstOrder) || final.SequenceEqual(secondOrder));
                    Assert.Equal(3, final.Length);
                    var user = await verify.Users.AsNoTracking().SingleAsync(item => item.Id == author);
                    Assert.Equal("Bếp Việt\nMỗi món ngon là một câu chuyện", user.Bio);
                    Assert.Equal("/img/recipes/pho.webp", user.CoverImageUrl);
                }

                var failingOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).AddInterceptors(new FailPinInsert()).Options;
                await using (var failing = new ApplicationDbContext(failingOptions))
                {
                    await Assert.ThrowsAsync<DbUpdateException>(() => new ProfileHighlightsService(failing).ReplaceAsync(author, new[] { recipes[1] }));
                }

                await using (var verify = new ApplicationDbContext(options))
                {
                    Assert.Equal(final, await PinsAsync(verify, author));
                    Assert.False(await new ProfileHighlightsService(verify).ReplaceAsync(author, recipes));
                    Assert.Equal(final, await PinsAsync(verify, author));
                }

                await using (var invalid = new ApplicationDbContext(options))
                {
                    int unpinned = recipes.Single(id => !final.Contains(id));
                    invalid.ProfileFeaturedRecipes.Add(new ProfileFeaturedRecipe { UserId = author, RecipeId = unpinned, DisplayOrder = 4 });
                    var error = await Assert.ThrowsAsync<DbUpdateException>(() => invalid.SaveChangesAsync());
                    Assert.Equal("CK_ProfileFeaturedRecipes_DisplayOrder", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
                }

                await using (var duplicate = new ApplicationDbContext(options))
                {
                    int unpinned = recipes.Single(id => !final.Contains(id));
                    duplicate.ProfileFeaturedRecipes.Add(new ProfileFeaturedRecipe { UserId = author, RecipeId = unpinned, DisplayOrder = 1 });
                    var error = await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
                    Assert.Equal("IX_ProfileFeaturedRecipes_UserId_DisplayOrder", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
                }

                await using (var deletion = new ApplicationDbContext(options))
                {
                    await deletion.Recipes.Where(recipe => recipe.Id == final[0] && recipe.UserId == author).ExecuteDeleteAsync();
                    Assert.Equal(final.Skip(1), await PinsAsync(deletion, author));
                    Assert.Equal(3, await deletion.Recipes.CountAsync(recipe => recipe.UserId == author));
                }
            }
            finally
            {
                await using var cleanup = new ApplicationDbContext(options);
                await cleanup.Recipes.Where(recipe => recipe.UserId == author).ExecuteDeleteAsync();
                await cleanup.Users.Where(user => user.Id == author).ExecuteDeleteAsync();
            }
        }

        [PostgresFact]
        public async Task Migration_ShouldPreserveLegacyProfilesAndStartWithNoFeaturedRecipes()
        {
            string connection = TestConnection();
            string databaseName = "cookshare_test_creator_" + Guid.NewGuid().ToString("N");
            var quote = new NpgsqlCommandBuilder();
            string quoted = quote.QuoteIdentifier(databaseName);
            await using var admin = new NpgsqlConnection(connection);
            await admin.OpenAsync();
            await using (var create = new NpgsqlCommand("CREATE DATABASE " + quoted, admin)) await create.ExecuteNonQueryAsync();
            try
            {
                var isolated = new NpgsqlConnectionStringBuilder(connection) { Database = databaseName, Pooling = false };
                var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.ConnectionString).Options;
                await using var db = new ApplicationDbContext(options);
                await db.GetService<IMigrator>().MigrateAsync("20261023183106_AddRecipeSourceMetadata");
                await db.Database.ExecuteSqlRawAsync("INSERT INTO \"AspNetUsers\" (\"Id\", \"UserName\", \"DisplayName\", \"AvatarUrl\", \"CreatedAt\", \"EmailConfirmed\", \"PhoneNumberConfirmed\", \"TwoFactorEnabled\", \"LockoutEnabled\", \"AccessFailedCount\") VALUES ('legacy-creator', 'legacy', 'Bếp cũ', '/img/avatar.webp', NOW(), FALSE, FALSE, FALSE, FALSE, 0)");
                await db.Database.MigrateAsync();
                var legacy = await db.Users.SingleAsync(user => user.Id == "legacy-creator");
                Assert.Equal("Bếp cũ", legacy.DisplayName);
                Assert.Equal("/img/avatar.webp", legacy.AvatarUrl);
                Assert.Null(legacy.Bio); Assert.Null(legacy.CoverImageUrl);
                Assert.Empty(await db.ProfileFeaturedRecipes.ToListAsync());
            }
            finally
            {
                // The name is generated here, never supplied by configuration or a request.
                Assert.StartsWith("cookshare_test_creator_", databaseName);
                await using var drop = new NpgsqlCommand("DROP DATABASE " + quoted + " WITH (FORCE)", admin);
                await drop.ExecuteNonQueryAsync();
            }
        }

        private static string TestConnection()
        {
            string connection = Environment.GetEnvironmentVariable("COOKSHARE_TEST_CONNECTION")!;
            string? name = new NpgsqlConnectionStringBuilder(connection).Database;
            Assert.True(name == "cookshare_test" || name?.StartsWith("cookshare_test_", StringComparison.Ordinal) == true);
            return connection;
        }

        private static Task<int[]> PinsAsync(ApplicationDbContext db, string userId) => db.ProfileFeaturedRecipes.AsNoTracking()
            .Where(pin => pin.UserId == userId).OrderBy(pin => pin.DisplayOrder).Select(pin => pin.RecipeId).ToArrayAsync();

        private sealed class LockGate : DbCommandInterceptor
        {
            private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _arrivals;
            public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                if (command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
                {
                    if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
                    await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                }

                return result;
            }
        }

        private sealed class FailPinInsert : SaveChangesInterceptor
        {
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (eventData.Context!.ChangeTracker.Entries<ProfileFeaturedRecipe>().Any(entry => entry.State == EntityState.Added)) throw new DbUpdateException("Simulated insert failure");
                return ValueTask.FromResult(result);
            }
        }
    }
}
