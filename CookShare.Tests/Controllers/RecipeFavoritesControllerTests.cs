using System.Data;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.Json;
using CookShare.Controllers;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CookShare.Tests.Controllers
{
    public class RecipeFavoritesControllerTests
    {
        [Fact]
        public void Endpoints_ShouldRequireAuthenticationCsrfAndPrivateCaching()
        {
            var type = typeof(RecipeFavoritesController);
            Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), true));
            var cache = Assert.Single(type.GetCustomAttributes(typeof(ResponseCacheAttribute), true)
                .Cast<ResponseCacheAttribute>());
            Assert.True(cache.NoStore);
            Assert.Equal(ResponseCacheLocation.None, cache.Location);
            foreach (string action in new[] { "Save", "Unsave" })
            {
                var method = type.GetMethod(action)!;
                Assert.Single(method.GetCustomAttributes(typeof(HttpPostAttribute), true));
                Assert.Single(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true));
                Assert.Empty(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
            }
        }

        [Theory]
        [InlineData(false, "owner")]
        [InlineData(true, null)]
        [InlineData(true, "")]
        public async Task MissingAuthenticatedIdentity_ShouldChallengeWithoutReadingOrWriting(bool authenticated, string? userId)
        {
            using var db = new ApplicationDbContext(NewOptions());
            var controller = CreateController(db, userId, authenticated);

            Assert.IsType<ChallengeResult>(await controller.Save(1));
            Assert.IsType<ChallengeResult>(await controller.Unsave(1));
            Assert.IsType<ChallengeResult>(await controller.State(new[] { 1 }));
            Assert.Empty(await db.Favorites.ToListAsync());
        }

        [Fact]
        public async Task SaveAndUnsave_ShouldBeIdempotentAndOnlyAffectTheCurrentAccount()
        {
            using var db = new ApplicationDbContext(NewOptions());
            Seed(db);
            db.Favorites.Add(new Favorite { RecipeId = 1, UserId = "other" });
            db.SaveChanges();
            var controller = CreateController(db);
            var before = DateTime.UtcNow.AddSeconds(-1);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                var json = Json(await controller.Save(1));
                Assert.True(json.GetProperty("isSaved").GetBoolean());
                Assert.Equal(1, json.GetProperty("savedCount").GetInt32());
                Assert.Equal(1, json.GetProperty("recipeId").GetInt32());
            }

            var favorites = await db.Favorites.AsNoTracking().ToListAsync();
            Assert.Equal(2, favorites.Count);
            var saved = Assert.Single(favorites.Where(favorite => favorite.UserId == "owner"));
            Assert.InRange(saved.CreatedAt, before, DateTime.UtcNow.AddSeconds(1));
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var json = Json(await controller.Unsave(1));
                Assert.False(json.GetProperty("isSaved").GetBoolean());
                Assert.Equal(0, json.GetProperty("savedCount").GetInt32());
            }

            Assert.Equal("other", Assert.Single(await db.Favorites.AsNoTracking().ToListAsync()).UserId);
            Assert.Single(await db.Recipes.ToListAsync());
        }

        [Fact]
        public async Task MissingRecipe_ShouldReturn404ForSaveAndUnsave()
        {
            using var db = new ApplicationDbContext(NewOptions());
            Seed(db);
            var controller = CreateController(db);

            Assert.IsType<NotFoundObjectResult>(await controller.Save(999));
            Assert.IsType<NotFoundObjectResult>(await controller.Unsave(999));
            Assert.Empty(await db.Favorites.ToListAsync());
        }

        [Fact]
        public async Task State_ShouldFilterRequestedIdsAndCountOnlyOwnFavoritesWithoutTracking()
        {
            using var db = new ApplicationDbContext(NewOptions());
            Seed(db);
            db.Recipes.Add(NewRecipe(2));
            db.Favorites.AddRange(
                new Favorite { RecipeId = 1, UserId = "owner" },
                new Favorite { RecipeId = 2, UserId = "owner" },
                new Favorite { RecipeId = 1, UserId = "other" });
            db.SaveChanges();
            db.ChangeTracker.Clear();
            var controller = CreateController(db);

            var json = Json(await controller.State(new[] { 1, 1, -1, 999 }));

            Assert.Equal("owner", json.GetProperty("userId").GetString());
            Assert.Equal(new[] { 1 }, json.GetProperty("savedRecipeIds").EnumerateArray().Select(id => id.GetInt32()));
            Assert.Equal(2, json.GetProperty("savedCount").GetInt32());
            Assert.Empty(db.ChangeTracker.Entries());
            var empty = Json(await controller.State(null));
            Assert.Empty(empty.GetProperty("savedRecipeIds").EnumerateArray());
            Assert.Equal(2, empty.GetProperty("savedCount").GetInt32());
        }

        [Fact]
        public async Task ConcurrentSave_UniqueIndexWinner_ShouldReturnDatabaseStateAndDetachFailedInsert()
        {
            var interceptor = new BeforeSaveInterceptor();
            var options = NewOptions(interceptor);
            using var db = new ApplicationDbContext(options);
            Seed(db);
            interceptor.BeforeSave = _ =>
            {
                using var winner = new ApplicationDbContext(options);
                winner.Favorites.Add(new Favorite { RecipeId = 1, UserId = "owner" });
                winner.SaveChanges();
                throw new DbUpdateException("private failure",
                    new PostgresException("duplicate", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
                        constraintName: "IX_Favorites_UserId_RecipeId"));
            };

            var json = Json(await CreateController(db).Save(1));

            Assert.True(json.GetProperty("isSaved").GetBoolean());
            Assert.Equal(1, json.GetProperty("savedCount").GetInt32());
            Assert.Single(await db.Favorites.AsNoTracking().ToListAsync());
            Assert.DoesNotContain(db.ChangeTracker.Entries<Favorite>(), entry => entry.State == EntityState.Added);
        }

        [Fact]
        public async Task ConcurrentUnsave_AlreadyDeletedRow_ShouldReturnDatabaseState()
        {
            var interceptor = new BeforeSaveInterceptor();
            var options = NewOptions(interceptor);
            using var db = new ApplicationDbContext(options);
            Seed(db);
            db.Favorites.Add(new Favorite { RecipeId = 1, UserId = "owner" });
            db.SaveChanges();
            interceptor.BeforeSave = _ =>
            {
                using var winner = new ApplicationDbContext(options);
                winner.Favorites.Remove(winner.Favorites.Single());
                winner.SaveChanges();
                throw new DbUpdateConcurrencyException("already removed");
            };

            var json = Json(await CreateController(db).Unsave(1));

            Assert.False(json.GetProperty("isSaved").GetBoolean());
            Assert.Equal(0, json.GetProperty("savedCount").GetInt32());
            Assert.Empty(await db.Favorites.AsNoTracking().ToListAsync());
        }

        [Theory]
        [InlineData("unrelated_unique_constraint")]
        [InlineData(null)]
        public async Task UnexpectedUniqueFailure_ShouldReturnSanitized503(string? constraint)
        {
            var interceptor = new BeforeSaveInterceptor();
            using var db = new ApplicationDbContext(NewOptions(interceptor));
            Seed(db);
            interceptor.BeforeSave = _ => throw new DbUpdateException("private password detail",
                new PostgresException("private database secret", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
                    constraintName: constraint));

            var result = Assert.IsType<ObjectResult>(await CreateController(db).Save(1));

            Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
            Assert.DoesNotContain("private", JsonSerializer.Serialize(result.Value));
            Assert.Empty(await db.Favorites.AsNoTracking().ToListAsync());
        }

        [Fact]
        public async Task DatabaseReadFailure_ShouldReturnSanitized503()
        {
            // A private EF service provider prevents an already compiled query from bypassing
            // this interceptor when another test has executed the same controller query.
            using var provider = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).UseInternalServiceProvider(provider)
                .AddInterceptors(new ThrowingQueryInterceptor()).Options;
            using var db = new ApplicationDbContext(options);
            var controller = CreateController(db);

            foreach (var result in new[] { await controller.Save(1), await controller.Unsave(1), await controller.State(new[] { 1 }) })
            {
                var unavailable = Assert.IsType<ObjectResult>(result);
                Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
                Assert.DoesNotContain("private", JsonSerializer.Serialize(unavailable.Value));
            }
        }

        [Fact]
        public async Task RecipeDeletedDuringSave_ShouldReturn404()
        {
            var interceptor = new BeforeSaveInterceptor();
            var options = NewOptions(interceptor);
            using var db = new ApplicationDbContext(options);
            Seed(db);
            interceptor.BeforeSave = _ =>
            {
                using var other = new ApplicationDbContext(options);
                other.Recipes.Remove(other.Recipes.Single());
                other.SaveChanges();
                throw new DbUpdateException("missing recipe",
                    new PostgresException("foreign key", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation));
            };

            Assert.IsType<NotFoundObjectResult>(await CreateController(db).Save(1));
            Assert.Empty(await db.Favorites.AsNoTracking().ToListAsync());
        }

        [Fact]
        public async Task OtherForeignKeyFailure_ShouldReturn503()
        {
            var interceptor = new BeforeSaveInterceptor();
            using var db = new ApplicationDbContext(NewOptions(interceptor));
            Seed(db);
            interceptor.BeforeSave = _ => throw new DbUpdateException("private user foreign key detail",
                new PostgresException("private foreign key", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation));

            var result = Assert.IsType<ObjectResult>(await CreateController(db).Save(1));

            Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
            Assert.DoesNotContain("private", JsonSerializer.Serialize(result.Value));
            Assert.Empty(await db.Favorites.AsNoTracking().ToListAsync());
        }

        [Fact]
        public void FavoritesQueryAndExistingUniqueIndex_ShouldTranslateForPostgreSqlWithoutConnecting()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=favorites_query_translation;Username=query_test")
                .Options;
            using var db = new ApplicationDbContext(options);
            int[] ids = { 1, 2 };

            string sql = db.Favorites.AsNoTracking()
                .Where(favorite => favorite.UserId == "owner" && ids.Contains(favorite.RecipeId))
                .Select(favorite => favorite.RecipeId).ToQueryString();

            Assert.Contains("Favorites", sql);
            Assert.Contains("UserId", sql);
            Assert.Contains("ANY", sql);
            Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
            Assert.True(db.Model.FindEntityType(typeof(Favorite))!.GetIndexes()
                .Single(candidate => candidate.Properties.Select(property => property.Name)
                    .SequenceEqual(new[] { "UserId", "RecipeId" })).IsUnique);
        }

        private static JsonElement Json(IActionResult result)
        {
            return JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value);
        }

        private static DbContextOptions<ApplicationDbContext> NewOptions(IInterceptor? interceptor = null)
        {
            var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString());
            if (interceptor != null)
            {
                builder.AddInterceptors(interceptor);
            }

            return builder.Options;
        }

        private static Recipe NewRecipe(int id)
        {
            return new Recipe
            {
                Id = id,
                UserId = "owner",
                Name = "Recipe",
                Description = "Description",
                Category = "Món Việt",
                Ingredients = "Ingredient",
                Instructions = "Step"
            };
        }

        private static void Seed(ApplicationDbContext db)
        {
            db.Users.AddRange(new ApplicationUser { Id = "owner", UserName = "owner" },
                new ApplicationUser { Id = "other", UserName = "other" });
            db.Recipes.Add(NewRecipe(1));
            db.SaveChanges();
        }

        private static RecipeFavoritesController CreateController(ApplicationDbContext db, string? userId = "owner", bool authenticated = true)
        {
            var claims = userId == null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
            var identity = new ClaimsIdentity(claims, authenticated ? "Test" : null);
            return new RecipeFavoritesController(db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
                }
            };
        }

        private sealed class BeforeSaveInterceptor : SaveChangesInterceptor
        {
            public Action<ApplicationDbContext>? BeforeSave { get; set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                BeforeSave?.Invoke((ApplicationDbContext)eventData.Context!);
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }

        private sealed class ThrowingQueryInterceptor : IQueryExpressionInterceptor
        {
            public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
            {
                throw new NpgsqlException("private connection password detail");
            }
        }
    }
}
