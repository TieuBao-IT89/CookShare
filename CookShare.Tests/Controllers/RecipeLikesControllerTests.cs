using System.Data;
using System.Security.Claims;
using System.Text.Json;
using CookShare.Controllers;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace CookShare.Tests.Controllers
{
    public class RecipeLikesControllerTests
    {
        [Fact]
        public async Task SimultaneousLike_UniqueIndexWinner_ShouldReturnPersistedState()
        {
            var interceptor = new BeforeSaveInterceptor();
            var options = NewOptions(interceptor);
            using var db = new ApplicationDbContext(options);
            Seed(db);
            interceptor.BeforeSave = _ =>
            {
                using var winner = new ApplicationDbContext(options);
                winner.RecipeLikes.Add(new RecipeLike { RecipeId = 1, UserId = "owner" });
                winner.SaveChanges();
                throw new DbUpdateException("private failure detail",
                    new PostgresException("duplicate", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
                        constraintName: "IX_RecipeLikes_UserId_RecipeId"));
            };

            var result = Assert.IsType<JsonResult>(await CreateController(db).Like(1));
            var json = JsonSerializer.SerializeToElement(result.Value);

            Assert.True(json.GetProperty("IsLiked").GetBoolean());
            Assert.Equal(1, json.GetProperty("LikeCount").GetInt32());
            Assert.Single(await db.RecipeLikes.AsNoTracking().ToListAsync());
            Assert.DoesNotContain(db.ChangeTracker.Entries<RecipeLike>(), entry => entry.State == EntityState.Added);
        }

        [Fact]
        public async Task SimultaneousUnlike_DeletedRow_ShouldReturnPersistedState()
        {
            var interceptor = new BeforeSaveInterceptor();
            var options = NewOptions(interceptor);
            using var db = new ApplicationDbContext(options);
            Seed(db);
            db.RecipeLikes.Add(new RecipeLike { RecipeId = 1, UserId = "owner" });
            db.SaveChanges();
            interceptor.BeforeSave = _ =>
            {
                using var winner = new ApplicationDbContext(options);
                winner.RecipeLikes.Remove(winner.RecipeLikes.Single());
                winner.SaveChanges();
                throw new DbUpdateConcurrencyException("already removed");
            };

            var result = Assert.IsType<JsonResult>(await CreateController(db).Unlike(1));
            var json = JsonSerializer.SerializeToElement(result.Value);

            Assert.False(json.GetProperty("IsLiked").GetBoolean());
            Assert.Equal(0, json.GetProperty("LikeCount").GetInt32());
            Assert.Empty(await db.RecipeLikes.AsNoTracking().ToListAsync());
        }

        [Theory]
        [InlineData("unrelated_unique_constraint")]
        [InlineData(null)]
        public async Task UnexpectedWriteFailure_ShouldNotPretendLikeSucceeded(string? constraint)
        {
            var interceptor = new BeforeSaveInterceptor();
            using var db = new ApplicationDbContext(NewOptions(interceptor));
            Seed(db);
            interceptor.BeforeSave = _ => throw new DbUpdateException("private failure detail",
                new PostgresException("private database details", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
                    constraintName: constraint));

            var result = Assert.IsType<ObjectResult>(await CreateController(db).Like(1));

            Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
            Assert.DoesNotContain("private", JsonSerializer.Serialize(result.Value));
            Assert.Empty(await db.RecipeLikes.AsNoTracking().ToListAsync());
        }

        [Fact]
        public async Task RecipeDeletedDuringLike_ShouldReturn404()
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

            Assert.IsType<NotFoundObjectResult>(await CreateController(db).Like(1));
            Assert.Empty(await db.RecipeLikes.AsNoTracking().ToListAsync());
        }

        [Fact]
        public async Task BatchedStates_ShouldCountActualLikesAndOnlyMatchCurrentViewer()
        {
            using var db = new ApplicationDbContext(NewOptions());
            Seed(db);
            db.RecipeLikes.AddRange(
                new RecipeLike { RecipeId = 1, UserId = "owner" },
                new RecipeLike { RecipeId = 1, UserId = "other" });
            db.SaveChanges();
            db.ChangeTracker.Clear();

            var ownerStates = await RecipeLikeQueries.LoadAsync(db, new[] { 1, 1, 999 }, "owner");
            var visitorStates = await RecipeLikeQueries.LoadAsync(db, new[] { 1 }, null);

            Assert.Single(ownerStates);
            Assert.Equal(2, ownerStates[1].LikeCount);
            Assert.True(ownerStates[1].IsLiked);
            Assert.False(visitorStates[1].IsLiked);
            Assert.Empty(db.ChangeTracker.Entries());
            Assert.Empty(await RecipeLikeQueries.LoadAsync(db, Array.Empty<int>(), "owner"));
        }

        [Fact]
        public void BatchedStates_ShouldTranslateToSinglePostgreSqlQueryWithoutConnecting()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=interaction_query_translation;Username=query_test")
                .Options;
            using var db = new ApplicationDbContext(options);

            string sql = db.Recipes.Where(recipe => recipe.Id == 1 || recipe.Id == 2)
                .SelectLikeStates("owner").ToQueryString();

            Assert.Contains("count(*)", sql);
            Assert.Contains("EXISTS", sql);
            Assert.Contains("RecipeLikes", sql);
            Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
            Assert.True(db.Model.FindEntityType(typeof(RecipeLike))!.GetIndexes()
                .Single(index => index.Properties.Select(property => property.Name)
                    .SequenceEqual(new[] { "UserId", "RecipeId" })).IsUnique);
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

        private static void Seed(ApplicationDbContext db)
        {
            db.Users.AddRange(
                new ApplicationUser { Id = "owner", UserName = "owner" },
                new ApplicationUser { Id = "other", UserName = "other" });
            db.Recipes.Add(new Recipe
            {
                Id = 1,
                UserId = "owner",
                Name = "Recipe",
                Description = "Description",
                Category = "Món Việt",
                Ingredients = "Ingredient",
                Instructions = "Step"
            });
            db.SaveChanges();
        }

        private static RecipeLikesController CreateController(ApplicationDbContext db)
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "owner") }, "Test");
            return new RecipeLikesController(db)
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
    }
}
