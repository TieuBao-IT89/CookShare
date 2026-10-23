using System.Security.Claims;
using CookShare.Controllers;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace CookShare.Tests.Integration
{
    [Collection(PostgreSqlIntegrationCollection.Name)]
    public sealed class RecipeRatingsPostgresTests
    {
        [PostgresFact]
        public async Task ConcurrentRatings_ShouldKeepOneRowAndPersistAcrossHostRestarts()
        {
            string connection = Environment.GetEnvironmentVariable("COOKSHARE_TEST_CONNECTION")!;
            string? database = new NpgsqlConnectionStringBuilder(connection).Database;
            Assert.True(database == "cookshare_test" || database?.StartsWith("cookshare_test_", StringComparison.Ordinal) == true);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
            string author = "rating-author-" + Guid.NewGuid().ToString("N");
            string reader = "rating-reader-" + Guid.NewGuid().ToString("N");
            int recipeId = 0;
            try
            {
                await using (var seed = new ApplicationDbContext(options))
                {
                    seed.Users.AddRange(new ApplicationUser { Id = author, UserName = author }, new ApplicationUser { Id = reader, UserName = reader });
                    var recipe = new Recipe { UserId = author, Name = "Durable rating recipe", Description = "Test recipe", Category = "Dinner", Ingredients = "Rice", Instructions = "Cook" };
                    seed.Recipes.Add(recipe);
                    await seed.SaveChangesAsync();
                    recipeId = recipe.Id;
                }

                var gate = new ConcurrentRatingGate();
                var concurrent = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).AddInterceptors(gate).Options;
                await using (var first = new ApplicationDbContext(concurrent))
                await using (var second = new ApplicationDbContext(concurrent))
                {
                    var results = await Task.WhenAll(
                        Controller(first, reader).Save(recipeId, new RecipeRatingFormViewModel { Stars = 3 }, default),
                        Controller(second, reader).Save(recipeId, new RecipeRatingFormViewModel { Stars = 5 }, default));
                    Assert.All(results, result => Assert.IsType<JsonResult>(result));
                }

                await using (var verify = new ApplicationDbContext(options))
                {
                    Assert.Single(await verify.RecipeRatings.Where(rating => rating.RecipeId == recipeId).ToListAsync());
                    Assert.IsType<JsonResult>(await Controller(verify, reader).Save(recipeId, new RecipeRatingFormViewModel { Stars = 4 }, default));
                }

                for (int restart = 0; restart < 2; restart++)
                {
                    await using var host = new CookShareWebApplicationFactory();
                    using var client = host.CreateTestClient(reader);
                    string html = await client.GetStringAsync($"/Recipe/Details/{recipeId}");
                    Assert.Contains("data-user-rating=\"4\"", html);
                    Assert.Contains("data-rating-count=\"1\"", html);
                    Assert.Contains("data-average-rating=\"4\"", html);
                }

                // Database constraints also reject writes which bypass MVC validation.
                await using (var invalid = new ApplicationDbContext(options))
                {
                    invalid.RecipeRatings.Add(new RecipeRating { RecipeId = recipeId, UserId = author, Stars = 6 });
                    var error = await Assert.ThrowsAsync<DbUpdateException>(() => invalid.SaveChangesAsync());
                    Assert.Equal("CK_RecipeRatings_Stars", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
                }

                await using (var duplicate = new ApplicationDbContext(options))
                {
                    duplicate.RecipeRatings.Add(new RecipeRating { RecipeId = recipeId, UserId = reader, Stars = 2 });
                    var error = await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
                    Assert.Equal("IX_RecipeRatings_UserId_RecipeId", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
                }
            }
            finally
            {
                await using var cleanup = new ApplicationDbContext(options);
                await cleanup.Recipes.Where(recipe => recipe.Id == recipeId && recipe.UserId == author).ExecuteDeleteAsync();
                await cleanup.Users.Where(user => user.Id == author || user.Id == reader).ExecuteDeleteAsync();
            }
        }

        private static RecipeRatingsController Controller(ApplicationDbContext db, string viewerId)
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, viewerId) }, "Test"))
            };
            context.Request.Headers.Accept = "application/json";
            return new RecipeRatingsController(db) { ControllerContext = new ControllerContext { HttpContext = context } };
        }

        private sealed class ConcurrentRatingGate : SaveChangesInterceptor
        {
            private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _arrivals;

            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (eventData.Context!.ChangeTracker.Entries<RecipeRating>().Any(entry => entry.State == EntityState.Added))
                {
                    if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
                    await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                }

                return result;
            }
        }
    }
}
