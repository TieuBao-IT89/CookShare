using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.Services;
using CookShare.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CookShare.Tests.Integration
{
    [Collection(PostgreSqlIntegrationCollection.Name)]
    public sealed class RecipeCollectionsPostgresTests
    {
        [DisposableFavoritesPostgresFact]
        public async Task ConcurrentMembersAndPublicProjection_ShouldPersistAndRespectPrivacyWithRealConstraints()
        {
            string connection = Environment.GetEnvironmentVariable("COOKSHARE_TEST_CONNECTION")!;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
            string ownerId = "collections-" + Guid.NewGuid().ToString("N");
            int recipeId = 0;
            int firstId = 0;
            int secondId = 0;
            try
            {
                await using (var db = new ApplicationDbContext(options))
                {
                    db.Users.Add(new ApplicationUser { Id = ownerId, UserName = ownerId });
                    var recipe = new Recipe { UserId = ownerId, Name = "Món bền vững", Description = "Test riêng", Category = "Món Việt", Ingredients = "Rau", Instructions = "Nấu" };
                    db.Recipes.Add(recipe);
                    await db.SaveChangesAsync();
                    recipeId = recipe.Id;
                    var service = new RecipeCollectionService(db);
                    firstId = (await service.CreateAsync(ownerId, new CollectionInputViewModel { Name = "Riêng tư A", IsPublic = true }))!.Id;
                    secondId = (await service.CreateAsync(ownerId, new CollectionInputViewModel { Name = "Riêng tư B" }))!.Id;
                    Assert.Empty(await service.PublicForOwnerAsync(ownerId));
                    Assert.Null(await service.ReadAsync(firstId, "another-account", 1));
                }

                var gate = new ConcurrentSaveGate();
                var racingOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).AddInterceptors(gate).Options;
                await using (var first = new ApplicationDbContext(racingOptions))
                await using (var second = new ApplicationDbContext(racingOptions))
                {
                    var results = await Task.WhenAll(
                        new RecipeCollectionService(first).SetMembershipAsync(ownerId, firstId, recipeId, true),
                        new RecipeCollectionService(second).SetMembershipAsync(ownerId, firstId, recipeId, true));
                    Assert.All(results, result => Assert.Equal(CollectionMutationResult.Success, result));
                }

                await using (var db = new ApplicationDbContext(options))
                {
                    var service = new RecipeCollectionService(db);
                    Assert.Single(await db.CollectionRecipes.Where(member => member.CollectionId == firstId).ToListAsync());
                    Assert.Equal(CollectionMutationResult.Success, await service.SetMembershipAsync(ownerId, secondId, recipeId, true));
                    db.Favorites.Add(new Favorite { UserId = ownerId, RecipeId = recipeId });
                    await db.SaveChangesAsync();
                    Assert.True(await service.EditAsync(ownerId, firstId, new CollectionInputViewModel { Name = "Công khai A", IsPublic = true }));
                    var summary = Assert.Single(await service.PublicForOwnerAsync(ownerId));
                    Assert.Equal(firstId, summary.Id);
                    Assert.Equal(1, summary.RecipeCount);
                    Assert.NotNull(await service.ReadAsync(firstId, null, 1));
                    Assert.Null(await service.ReadAsync(secondId, null, 1));
                    Assert.True(await service.DeleteAsync(ownerId, firstId));
                    Assert.Single(await db.CollectionRecipes.Where(member => member.CollectionId == secondId).ToListAsync());
                    Assert.Single(await db.Favorites.Where(favorite => favorite.UserId == ownerId).ToListAsync());
                    Assert.Single(await db.Recipes.Where(recipe => recipe.UserId == ownerId).ToListAsync());
                }
            }
            finally
            {
                await using var cleanup = new ApplicationDbContext(options);
                await cleanup.RecipeCollections.Where(collection => collection.OwnerId == ownerId).ExecuteDeleteAsync();
                await cleanup.Favorites.IgnoreQueryFilters().Where(favorite => favorite.UserId == ownerId).ExecuteDeleteAsync();
                await cleanup.Recipes.IgnoreQueryFilters().Where(recipe => recipe.UserId == ownerId).ExecuteDeleteAsync();
                await cleanup.Users.Where(user => user.Id == ownerId).ExecuteDeleteAsync();
            }
        }

        private sealed class ConcurrentSaveGate : SaveChangesInterceptor
        {
            private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _arrivals;

            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
                InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                return result;
            }
        }
    }
}
