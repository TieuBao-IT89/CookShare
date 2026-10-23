using System.Globalization;
using System.Text.Json;
using CookShare.Data.Seeding;
using CookShare.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CookShare.Tests.Seeding
{
    public sealed class RecipeSeederTests
    {
        [Fact]
        public async Task LocalSeed_ShouldPersistThirtyValidRecipesAndKeepUserEditsOnRerun()
        {
            await using var db = RecipeSeedTestSupport.CreateDb();
            await RecipeSeedTestSupport.AddUsersAsync(db);
            var source = new LocalRecipeSeedSource(new SeedEnvironment());
            var seeder = RecipeSeedTestSupport.Seeder(db, source);
            var first = await seeder.RunAsync(new RecipeSeedOptions());

            Assert.Equal(30, first.Added);
            Assert.Equal(0, first.Failed);
            var recipes = await db.Recipes.Include(recipe => recipe.User).ToListAsync();
            Assert.Equal(15, recipes.Count(recipe => recipe.User!.UserName == "admintest1"));
            Assert.Equal(15, recipes.Count(recipe => recipe.User!.UserName == "root123"));
            Assert.Equal(30, recipes.Select(recipe => recipe.ExternalId).Distinct().Count());
            Assert.All(recipes, recipe =>
            {
                Assert.Equal("local", recipe.Source);
                Assert.Equal(DateTimeKind.Utc, recipe.CreatedAt.Kind);
                Assert.False(string.IsNullOrWhiteSpace(recipe.Name));
                Assert.False(string.IsNullOrWhiteSpace(recipe.Description));
                Assert.False(string.IsNullOrWhiteSpace(recipe.Ingredients));
                Assert.False(string.IsNullOrWhiteSpace(recipe.Instructions));
                Assert.Contains(recipe.Category, RecipeSeedRecord.Categories);
                // Static files belong to the web project, so verify against its source tree.
                string imagePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../CookShare/wwwroot", recipe.Image!.TrimStart('/')));
                Assert.True(File.Exists(imagePath), imagePath);
            });

            recipes[0].Name = "A user edited this seeded recipe";
            string? originalOwner = recipes[0].UserId;
            db.Recipes.Add(new CookShare.Models.Recipe
            {
                Name = "Real user recipe",
                Description = "Keep me",
                Ingredients = "Rice",
                Instructions = "Cook",
                Category = "Món Việt"
            });
            await db.SaveChangesAsync();
            var repeated = await seeder.RunAsync(new RecipeSeedOptions { UserNames = new[] { "ROOT123", "ADMINTEST1" } });

            Assert.Equal(0, repeated.Added);
            Assert.Equal(30, repeated.Skipped);
            Assert.Equal(31, await db.Recipes.CountAsync());
            Assert.Equal("A user edited this seeded recipe", recipes[0].Name);
            Assert.Equal(originalOwner, recipes[0].UserId);
            Assert.Equal("Keep me", (await db.Recipes.SingleAsync(recipe => recipe.Name == "Real user recipe")).Description);
        }

        [Fact]
        public async Task DryRun_ShouldPreviewAssignmentsWithoutWritingOrTrackingNewRecipes()
        {
            await using var db = RecipeSeedTestSupport.CreateDb();
            await RecipeSeedTestSupport.AddUsersAsync(db);
            var seeder = RecipeSeedTestSupport.Seeder(db, new LocalRecipeSeedSource(new SeedEnvironment()));
            var result = await seeder.RunAsync(new RecipeSeedOptions { DryRun = true });

            Assert.Equal(30, result.WouldAdd);
            Assert.Equal(0, result.Added);
            Assert.Equal(15, result.Preview.Count(item => item.UserName == "admintest1"));
            Assert.Equal(15, result.Preview.Count(item => item.UserName == "root123"));
            Assert.Empty(await db.Recipes.ToListAsync());
            Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State == EntityState.Added);
        }

        [Fact]
        public async Task MissingUser_ShouldAbortBeforeReadingSourceAndNeverCreateUsers()
        {
            await using var db = RecipeSeedTestSupport.CreateDb();
            await RecipeSeedTestSupport.AddUsersAsync(db);
            db.Users.Remove(await db.Users.SingleAsync(user => user.UserName == "root123"));
            await db.SaveChangesAsync();
            var source = new StubRecipeSeedSource(RecipeSeedTestSupport.Record());

            var result = await RecipeSeedTestSupport.Seeder(db, source).RunAsync(new RecipeSeedOptions());

            Assert.Contains(result.Errors, error => error.Contains("root123", StringComparison.Ordinal));
            Assert.Equal(0, source.Calls);
            Assert.Empty(await db.Recipes.ToListAsync());
            Assert.Equal(1, await db.Users.CountAsync());
        }

        [Theory]
        [InlineData("Production")]
        [InlineData("Staging")]
        [InlineData("Custom")]
        public async Task UnsafeEnvironment_ShouldRejectEvenDryRunBeforeAccessingDatabase(string environment)
        {
            var db = RecipeSeedTestSupport.CreateDb();
            await db.DisposeAsync();
            var source = new StubRecipeSeedSource();
            var seeder = RecipeSeedTestSupport.Seeder(db, source, environment);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                seeder.RunAsync(new RecipeSeedOptions { DryRun = true }));

            Assert.Contains("Development or Testing", error.Message);
            Assert.Equal(0, source.Calls);
        }

        [Fact]
        public async Task InvalidAndDuplicateRecords_ShouldBeReportedWithoutLosingValidEntries()
        {
            await using var db = RecipeSeedTestSupport.CreateDb();
            await RecipeSeedTestSupport.AddUsersAsync(db);
            var valid = RecipeSeedTestSupport.Record();
            var source = new StubRecipeSeedSource(valid, valid,
                valid with { Key = "bad-name", Name = " " },
                valid with { Key = "bad-category", Category = "Unknown" },
                valid with { Key = "bad-image", Image = "javascript:alert(1)" },
                valid with { Key = "", Instructions = "" });

            var result = await RecipeSeedTestSupport.Seeder(db, source).RunAsync(new RecipeSeedOptions());

            Assert.Equal(1, result.Added);
            Assert.Equal(1, result.Skipped);
            Assert.Equal(4, result.Failed);
            Assert.Equal(RecipeImageReference.Placeholder, (await db.Recipes.SingleAsync()).Image);
        }

        [Fact]
        public async Task CountAndSingleOwner_ShouldLimitSelection()
        {
            await using var db = RecipeSeedTestSupport.CreateDb();
            await RecipeSeedTestSupport.AddUsersAsync(db);
            var source = new LocalRecipeSeedSource(new SeedEnvironment());
            var result = await RecipeSeedTestSupport.Seeder(db, source).RunAsync(new RecipeSeedOptions
            {
                Count = 3,
                UserNames = new[] { "ROOT123" }
            });

            Assert.Equal(3, result.Added);
            Assert.All(await db.Recipes.Include(recipe => recipe.User).ToListAsync(), recipe => Assert.Equal("root123", recipe.User!.UserName));
        }

        [Theory]
        [InlineData("http")]
        [InlineData("timeout")]
        [InlineData("json")]
        public async Task Command_SourceFailure_ShouldReturnFailureWithoutDatabaseWritesOrLeakingErrorDetails(string failure)
        {
            await using var db = RecipeSeedTestSupport.CreateDb();
            await RecipeSeedTestSupport.AddUsersAsync(db);
            Exception exception = failure switch
            {
                "http" => new HttpRequestException("private-api-key"),
                "timeout" => new TaskCanceledException("private-api-key"),
                _ => new JsonException("private-api-key")
            };
            var source = new StubRecipeSeedSource { Failure = exception };
            using var services = new ServiceCollection()
                .AddSingleton(RecipeSeedTestSupport.Seeder(db, source)).BuildServiceProvider();
            using var output = new StringWriter(CultureInfo.InvariantCulture);

            int exitCode = await RecipeSeedCommand.RunAsync(Array.Empty<string>(), services, output);

            Assert.Equal(1, exitCode);
            Assert.Empty(await db.Recipes.ToListAsync());
            Assert.DoesNotContain("private-api-key", output.ToString());
        }

        [Fact]
        public async Task Command_DryRunAndRerun_ShouldReportCountsAndExitSuccessfully()
        {
            await using var db = RecipeSeedTestSupport.CreateDb();
            await RecipeSeedTestSupport.AddUsersAsync(db);
            using var services = new ServiceCollection().AddSingleton(RecipeSeedTestSupport.Seeder(db,
                new LocalRecipeSeedSource(new SeedEnvironment()))).BuildServiceProvider();
            using var output = new StringWriter(CultureInfo.InvariantCulture);

            Assert.Equal(0, await RecipeSeedCommand.RunAsync(new[] { "--dry-run", "--count", "2" }, services, output));
            Assert.Contains("would add: 2", output.ToString());
            Assert.Contains("Phở bò", output.ToString());
            Assert.Equal(0, await db.Recipes.CountAsync());
            Assert.Equal(0, await RecipeSeedCommand.RunAsync(new[] { "--count", "2" }, services, output));
            Assert.Equal(0, await RecipeSeedCommand.RunAsync(new[] { "--count", "2" }, services, output));
            Assert.Contains("skipped: 2", output.ToString());
            Assert.Equal(2, await db.Recipes.CountAsync());
        }

        [Fact]
        public async Task Command_UnconfirmedDatabaseSave_ShouldNotClaimThatTheBatchWasRolledBack()
        {
            var interceptor = new UnconfirmedSaveInterceptor();
            var options = new DbContextOptionsBuilder<CookShare.Data.ApplicationDbContext>()
                .UseInMemoryDatabase("unconfirmed-seed-" + Guid.NewGuid()).AddInterceptors(interceptor).Options;
            await using var db = new CookShare.Data.ApplicationDbContext(options);
            await RecipeSeedTestSupport.AddUsersAsync(db);
            interceptor.FailSaves = true;
            using var services = new ServiceCollection().AddSingleton(RecipeSeedTestSupport.Seeder(db,
                new StubRecipeSeedSource(RecipeSeedTestSupport.Record()))).BuildServiceProvider();
            using var output = new StringWriter(CultureInfo.InvariantCulture);

            int exitCode = await RecipeSeedCommand.RunAsync(Array.Empty<string>(), services, output);

            Assert.Equal(1, exitCode);
            Assert.Contains("No successful import was reported", output.ToString());
            Assert.DoesNotContain("no recipes from this batch were added", output.ToString());
            Assert.DoesNotContain("private-connection", output.ToString());
        }

        private sealed class UnconfirmedSaveInterceptor : SaveChangesInterceptor
        {
            public bool FailSaves { get; set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (FailSaves)
                {
                    throw new DbUpdateException("private-connection", new NpgsqlException("private-connection"));
                }

                return ValueTask.FromResult(result);
            }
        }
    }
}
