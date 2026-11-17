using CookShare.Data;
using CookShare.Data.Seeding;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.Tests.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace CookShare.Tests.Integration
{
    [Collection(PostgreSqlIntegrationCollection.Name)]
    public sealed class RecipeSeedingPostgresTests
    {
        [PostgresFact]
        public async Task Seed_ShouldPersistAcrossContextsAndKeepExistingRecipesAndAssignments()
        {
            string[] owners = await CreateOwnersAsync();
            try
            {
                await using (var db = CreateDb())
                {
                    Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                    db.Recipes.Add(new Recipe
                    {
                        Name = "Existing user recipe",
                        Description = "Do not overwrite",
                        Ingredients = "Rice",
                        Instructions = "Cook",
                        Category = "Món Việt",
                        UserId = owners[0]
                    });
                    await db.SaveChangesAsync();
                    var result = await Seeder(db, owners[0]).RunAsync(new RecipeSeedOptions { UserNames = owners });
                    Assert.Equal(30, result.Added);
                    Assert.Equal(0, result.Failed);
                }

                await using (var db = CreateDb())
                {
                    var imported = await db.Recipes.Where(recipe => recipe.Source == "local" && owners.Contains(recipe.UserId!)).ToListAsync();
                    Assert.Equal(30, imported.Count);
                    Assert.Equal(15, imported.Count(recipe => recipe.UserId == owners[0]));
                    Assert.Equal(15, imported.Count(recipe => recipe.UserId == owners[1]));
                    imported[0].Name = "User changed a seeded recipe";
                    await db.SaveChangesAsync();
                    var result = await Seeder(db, owners[0]).RunAsync(new RecipeSeedOptions { UserNames = owners.Reverse().ToArray() });
                    Assert.Equal(0, result.Added);
                    Assert.Equal(30, result.Skipped);
                }

                await using (var db = CreateDb())
                {
                    Assert.Equal(31, await db.Recipes.CountAsync(recipe => owners.Contains(recipe.UserId!)));
                    Assert.True(await db.Recipes.AnyAsync(recipe => recipe.Name == "User changed a seeded recipe"));
                    Assert.True(await db.Recipes.AnyAsync(recipe => recipe.Source == null && recipe.Description == "Do not overwrite"));
                    Assert.Equal(15, await db.Recipes.CountAsync(recipe => recipe.Source == "local" && recipe.UserId == owners[0]));
                }
            }
            finally
            {
                await CleanupAsync(owners);
            }
        }

        [PostgresFact]
        public async Task ConcurrentSeeds_ShouldCommitOnlyOneBatchAndRollBackTheOther()
        {
            string[] owners = await CreateOwnersAsync();
            try
            {
                var gate = new ConcurrentSeedGate();
                await using var first = CreateDb(gate);
                await using var second = CreateDb(gate);
                var options = new RecipeSeedOptions { UserNames = owners };

                var results = await Task.WhenAll(Seeder(first, owners[0]).RunAsync(options), Seeder(second, owners[0]).RunAsync(options));

                Assert.Equal(30, results.Sum(result => result.Added));
                Assert.Equal(30, results.Sum(result => result.Failed));
                Assert.Contains(results, result => result.Added == 0 && result.Errors.Any());
                await using var verify = CreateDb();
                var imported = await verify.Recipes.Where(recipe => owners.Contains(recipe.UserId!)).ToListAsync();
                Assert.Equal(30, imported.Count);
                Assert.Equal(30, imported.Select(recipe => recipe.ExternalId).Distinct().Count());
                Assert.Equal(15, imported.Count(recipe => recipe.UserId == owners[0]));
                Assert.Equal(15, imported.Count(recipe => recipe.UserId == owners[1]));
            }
            finally
            {
                await CleanupAsync(owners);
            }
        }

        private static ApplicationDbContext CreateDb(SaveChangesInterceptor? interceptor = null)
        {
            string connection = Environment.GetEnvironmentVariable("COOKSHARE_TEST_CONNECTION")!;
            string? database = new NpgsqlConnectionStringBuilder(connection).Database;
            if (database != "cookshare_test" && database?.StartsWith("cookshare_test_", StringComparison.Ordinal) != true)
            {
                throw new InvalidOperationException("Seeding integration tests require a disposable cookshare_test or cookshare_test_* database.");
            }

            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection);
            if (interceptor != null)
            {
                options.AddInterceptors(interceptor);
            }

            return new ApplicationDbContext(options.Options);
        }

        private static RecipeSeeder Seeder(ApplicationDbContext db, string keyPrefix) => RecipeSeedTestSupport.Seeder(db,
            new IsolatedLocalSource(keyPrefix));

        private static async Task<string[]> CreateOwnersAsync()
        {
            string suffix = Guid.NewGuid().ToString("N");
            string[] owners = { "seed-one-" + suffix, "seed-two-" + suffix };
            await using var db = CreateDb();
            foreach (string name in owners)
            {
                db.Users.Add(new ApplicationUser
                {
                    Id = name,
                    UserName = name,
                    NormalizedUserName = new UpperInvariantLookupNormalizer().NormalizeName(name)
                });
            }

            await db.SaveChangesAsync();
            return owners;
        }

        private static async Task CleanupAsync(string[] owners)
        {
            // These random accounts and their recipes belong only to this test.
            await using var db = CreateDb();
            await db.Recipes.Where(recipe => owners.Contains(recipe.UserId!)).ExecuteDeleteAsync();
            await db.Users.Where(user => owners.Contains(user.Id)).ExecuteDeleteAsync();
        }

        private sealed class IsolatedLocalSource(string keyPrefix) : IRecipeSeedSource
        {
            public string Name => "local";

            public async Task<RecipeSeedBatch> ReadAsync(RecipeSeedOptions options, CancellationToken cancellationToken = default)
            {
                var batch = await new LocalRecipeSeedSource(new SeedEnvironment()).ReadAsync(options, cancellationToken);
                // Keep tests independent of samples already imported by a CLI smoke test or another run.
                return new RecipeSeedBatch(batch.Recipes.Select(recipe => recipe with { Key = keyPrefix + "-" + recipe.Key }).ToList(), batch.Errors);
            }
        }

        private sealed class ConcurrentSeedGate : SaveChangesInterceptor
        {
            private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _arrivals;

            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (Interlocked.Increment(ref _arrivals) == 2)
                {
                    _ready.TrySetResult();
                }

                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                return result;
            }
        }
    }
}
