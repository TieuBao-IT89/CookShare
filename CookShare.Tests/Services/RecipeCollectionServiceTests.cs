using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.Services;
using CookShare.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace CookShare.Tests.Services
{
    public sealed class RecipeCollectionServiceTests
    {
        [Fact]
        public async Task MembershipUniqueConstraintRace_ShouldDetachLosingInsertAndRemainIdempotent()
        {
            var race = new SaveRaceInterceptor();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(race).Options;
            using var db = new ApplicationDbContext(options);
            Seed(db);
            race.BeforeSave = () =>
            {
                using var winner = new ApplicationDbContext(options);
                winner.Set<CollectionRecipe>().Add(new CollectionRecipe { CollectionId = 1, RecipeId = 1 });
                winner.SaveChanges();
                throw new DbUpdateException("private duplicate details", new PostgresException("duplicate", "ERROR", "ERROR",
                    PostgresErrorCodes.UniqueViolation, constraintName: "PK_CollectionRecipes"));
            };

            var result = await new RecipeCollectionService(db).SetMembershipAsync("owner", 1, 1, true);

            Assert.Equal(CollectionMutationResult.Success, result);
            Assert.Single(await db.Set<CollectionRecipe>().AsNoTracking().ToListAsync());
            Assert.DoesNotContain(db.ChangeTracker.Entries<CollectionRecipe>(), entry => entry.State == EntityState.Added);
        }

        [Fact]
        public async Task UnrelatedUniqueFailure_ShouldNotBeSilentlyTreatedAsAnExistingMembership()
        {
            var race = new SaveRaceInterceptor();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(race).Options;
            using var db = new ApplicationDbContext(options);
            Seed(db);
            race.BeforeSave = () => throw new DbUpdateException("unexpected", new PostgresException("duplicate", "ERROR", "ERROR",
                PostgresErrorCodes.UniqueViolation, constraintName: "DifferentConstraint"));

            await Assert.ThrowsAsync<DbUpdateException>(() => new RecipeCollectionService(db).SetMembershipAsync("owner", 1, 1, true));
        }

        [Fact]
        public async Task ConcurrentRemoval_ShouldReturnSuccessWhenAnotherRequestAlreadyRemovedTheMember()
        {
            var race = new SaveRaceInterceptor();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(race).Options;
            using var db = new ApplicationDbContext(options);
            Seed(db);
            db.Set<CollectionRecipe>().Add(new CollectionRecipe { CollectionId = 1, RecipeId = 1 });
            db.SaveChanges();
            race.BeforeSave = () =>
            {
                using var winner = new ApplicationDbContext(options);
                winner.Set<CollectionRecipe>().Remove(winner.Set<CollectionRecipe>().Single());
                winner.SaveChanges();
                throw new DbUpdateConcurrencyException("already removed");
            };

            Assert.Equal(CollectionMutationResult.Success, await new RecipeCollectionService(db).SetMembershipAsync("owner", 1, 1, false));
            Assert.Empty(await db.Set<CollectionRecipe>().AsNoTracking().ToListAsync());
        }

        [Fact]
        public async Task PublicProjection_ShouldExcludePrivateCollectionsAndHiddenCountAndCover()
        {
            using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Seed(db);
            db.Set<RecipeCollection>().Add(new RecipeCollection { Id = 2, OwnerId = "owner", Name = "Public", IsPublic = true });
            db.Recipes.Add(new Recipe { Id = 2, UserId = "owner", Name = "Hidden", Description = "Hidden", Ingredients = "x", Instructions = "x", Category = "x", IsHidden = true, Image = "/hidden.jpg" });
            db.Set<CollectionRecipe>().AddRange(new CollectionRecipe { CollectionId = 2, RecipeId = 1 }, new CollectionRecipe { CollectionId = 2, RecipeId = 2 });
            db.SaveChanges();
            db.ChangeTracker.Clear();

            var summary = Assert.Single(await new RecipeCollectionService(db).PublicForOwnerAsync("owner"));

            Assert.Equal(2, summary.Id);
            Assert.Equal(1, summary.RecipeCount);
            Assert.NotEqual("/hidden.jpg", summary.CoverImage);
            Assert.Empty(db.ChangeTracker.Entries());
        }

        [Fact]
        public async Task RelationalCascade_ShouldDeleteOnlyChosenCollectionAndKeepRecipeFavoriteAndOtherMembership()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            Seed(db);
            db.Set<RecipeCollection>().Add(new RecipeCollection { Id = 2, OwnerId = "owner", Name = "Other" });
            db.Set<CollectionRecipe>().AddRange(new CollectionRecipe { CollectionId = 1, RecipeId = 1 }, new CollectionRecipe { CollectionId = 2, RecipeId = 1 });
            db.Favorites.Add(new Favorite { RecipeId = 1, UserId = "owner" });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            Assert.True(await new RecipeCollectionService(db).DeleteAsync("owner", 1));

            Assert.Equal(2, Assert.Single(await db.Set<CollectionRecipe>().AsNoTracking().ToListAsync()).CollectionId);
            Assert.Single(await db.Recipes.ToListAsync());
            Assert.Single(await db.Favorites.ToListAsync());
            Assert.False(await db.Set<RecipeCollection>().AnyAsync(collection => collection.Id == 1));
        }

        [Fact]
        public async Task InvalidInitialRecipe_ShouldCreateNeitherCollectionNorMember()
        {
            using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Seed(db);

            var created = await new RecipeCollectionService(db).CreateAsync("owner", new CollectionInputViewModel { Name = "Invalid", RecipeId = 999 });

            Assert.Null(created);
            Assert.Single(await db.Set<RecipeCollection>().ToListAsync());
            Assert.Empty(await db.Set<CollectionRecipe>().ToListAsync());
        }

        private static void Seed(ApplicationDbContext db)
        {
            db.Users.Add(new ApplicationUser { Id = "owner", UserName = "owner" });
            db.Recipes.Add(new Recipe { Id = 1, UserId = "owner", Name = "Visible", Description = "Visible", Ingredients = "x", Instructions = "x", Category = "x" });
            db.Set<RecipeCollection>().Add(new RecipeCollection { Id = 1, OwnerId = "owner", Name = "Private" });
            db.SaveChanges();
        }

        private sealed class SaveRaceInterceptor : SaveChangesInterceptor
        {
            public Action? BeforeSave { get; set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
                InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                BeforeSave?.Invoke();
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }
    }
}
