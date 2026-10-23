using System.Security.Claims;
using System.Text.Json;
using CookShare.Controllers;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace CookShare.Tests.Integration
{
    public sealed class RecipeFavoritesPostgresTests
    {
        // Explicit opt-in only: provision the existing migrations separately in a disposable
        // database named cookshare_test_*. This suite never migrates or resets any database.
        [DisposableFavoritesPostgresFact]
        public async Task ConcurrentMutations_ShouldRespectUniqueIndexAndSurviveWebHostRestart()
        {
            string connection = Environment.GetEnvironmentVariable("COOKSHARE_TEST_CONNECTION")!;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
            string userId = "favorites-test-" + Guid.NewGuid().ToString("N");
            int recipeId = 0;
            try
            {
                await using (var seed = new ApplicationDbContext(options))
                {
                    seed.Users.Add(new ApplicationUser { Id = userId, UserName = userId });
                    var recipe = new Recipe
                    {
                        UserId = userId,
                        Name = "Durable PostgreSQL favorite " + userId,
                        Description = "Disposable database test recipe",
                        Category = "Dinner",
                        Ingredients = "Ingredient",
                        Instructions = "Cook"
                    };
                    seed.Recipes.Add(recipe);
                    await seed.SaveChangesAsync();
                    recipeId = recipe.Id;
                }

                var insertGate = new ConcurrentSaveGate();
                var insertOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseNpgsql(connection).AddInterceptors(insertGate).Options;
                await using (var first = new ApplicationDbContext(insertOptions))
                await using (var second = new ApplicationDbContext(insertOptions))
                {
                    IActionResult[] saved = await Task.WhenAll(
                        CreateController(first, userId).Save(recipeId),
                        CreateController(second, userId).Save(recipeId));
                    Assert.All(saved, result => Assert.True(State(result).GetProperty("isSaved").GetBoolean()));
                }

                await using (var verify = new ApplicationDbContext(options))
                {
                    Assert.Equal(1, await verify.Favorites.CountAsync(favorite => favorite.UserId == userId));
                }

                // Each factory owns a separate host and request scope. Neither host initializes
                // the schema; the second must read the row committed before the first shut down.
                foreach (int restart in new[] { 0, 1 })
                {
                    await using var host = new FavoritesPostgresWebApplicationFactory(connection);
                    using var client = host.CreateClient(new WebApplicationFactoryClientOptions
                    {
                        AllowAutoRedirect = false,
                        BaseAddress = new Uri("https://localhost")
                    });
                    client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
                    string html = await client.GetStringAsync("/Recipe/Saved");
                    Assert.Contains($"data-recipe-id=\"{recipeId}\"", html);
                    Assert.Contains("Durable PostgreSQL favorite " + userId, html);
                }

                var deleteGate = new ConcurrentSaveGate();
                var deleteOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseNpgsql(connection).AddInterceptors(deleteGate).Options;
                await using (var first = new ApplicationDbContext(deleteOptions))
                await using (var second = new ApplicationDbContext(deleteOptions))
                {
                    IActionResult[] unsaved = await Task.WhenAll(
                        CreateController(first, userId).Unsave(recipeId),
                        CreateController(second, userId).Unsave(recipeId));
                    Assert.All(unsaved, result => Assert.False(State(result).GetProperty("isSaved").GetBoolean()));
                }

                await using var final = new ApplicationDbContext(options);
                Assert.False(await final.Favorites.AnyAsync(favorite => favorite.UserId == userId));
                Assert.True(await final.Recipes.AnyAsync(recipe => recipe.Id == recipeId));
            }
            finally
            {
                // Delete only rows created by this random, private test account.
                await using var cleanup = new ApplicationDbContext(options);
                await cleanup.Favorites.Where(favorite => favorite.UserId == userId).ExecuteDeleteAsync();
                await cleanup.Recipes.Where(recipe => recipe.UserId == userId).ExecuteDeleteAsync();
                await cleanup.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
            }
        }

        private static JsonElement State(IActionResult result)
        {
            return JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value);
        }

        private static RecipeFavoritesController CreateController(ApplicationDbContext db, string userId)
        {
            return new RecipeFavoritesController(db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "Test"))
                    }
                }
            };
        }

        private sealed class ConcurrentSaveGate : SaveChangesInterceptor
        {
            private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _arrivals;

            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                if (Interlocked.Increment(ref _arrivals) == 2)
                {
                    _ready.TrySetResult();
                }

                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                return result;
            }
        }

        private sealed class FavoritesPostgresWebApplicationFactory(string connection) : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ApplicationDbContext>();
                    services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                    services.RemoveAll<DbContextOptions>();
                    services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connection));
                    services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, RecipeTestAuthHandler>(
                        "FavoritesPostgresAuth", _ => { });
                    services.PostConfigure<AuthenticationOptions>(options =>
                    {
                        options.DefaultAuthenticateScheme = "FavoritesPostgresAuth";
                        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                    });
                });
            }
        }
    }

    public sealed class DisposableFavoritesPostgresFactAttribute : FactAttribute
    {
        public DisposableFavoritesPostgresFactAttribute()
        {
            string? connection = Environment.GetEnvironmentVariable("COOKSHARE_TEST_CONNECTION");
            bool enabled = Environment.GetEnvironmentVariable("COOKSHARE_RUN_POSTGRES_TESTS") == "1";
            try
            {
                enabled = enabled && !string.IsNullOrWhiteSpace(connection)
                    && new NpgsqlConnectionStringBuilder(connection).Database?.StartsWith(
                        "cookshare_test_", StringComparison.OrdinalIgnoreCase) == true;
            }
            catch (ArgumentException)
            {
                enabled = false;
            }

            if (!enabled)
            {
                Skip = "Requires explicit COOKSHARE_RUN_POSTGRES_TESTS=1 and COOKSHARE_TEST_CONNECTION for a pre-provisioned disposable cookshare_test_* database.";
            }
        }
    }
}
