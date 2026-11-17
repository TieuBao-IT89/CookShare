using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.Services;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Tests.Services
{
    public class NotificationServiceTests
    {
        [Fact]
        public async Task LikeEvent_CreatesOneNotificationAndSkipsRecipeOwner()
        {
            await using var db = CreateDb();
            var recipe = NewRecipe();
            db.Users.AddRange(
                new ApplicationUser { Id = "owner" },
                new ApplicationUser { Id = "actor" });
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();

            var like = new RecipeLike
            {
                Id = 11,
                UserId = "actor",
                RecipeId = recipe.Id,
                Recipe = recipe
            };
            var service = new NotificationService(db);

            await service.NotifyRecipeLikeAsync(like);
            await service.NotifyRecipeLikeAsync(like);

            var notification = Assert.Single(await db.Notifications.ToListAsync());
            Assert.Equal("owner", notification.RecipientId);
            Assert.Equal("actor", notification.ActorId);
            Assert.Equal("Like", notification.Type);
            Assert.Equal("recipe-like:11", notification.EventKey);

            await service.NotifyRecipeLikeAsync(new RecipeLike
            {
                Id = 12,
                UserId = "owner",
                RecipeId = recipe.Id,
                Recipe = recipe
            });

            Assert.Single(await db.Notifications.ToListAsync());
        }

        [Fact]
        public async Task FollowEvent_StoresFollowTypeAndDeduplicatesByEventKey()
        {
            await using var db = CreateDb();
            db.Users.AddRange(
                new ApplicationUser { Id = "recipient" },
                new ApplicationUser { Id = "actor" });
            await db.SaveChangesAsync();

            var follow = new Follow
            {
                Id = 25,
                FollowerId = "actor",
                FollowingId = "recipient"
            };
            var service = new NotificationService(db);

            await service.NotifyFollowAsync(follow);
            await service.NotifyFollowAsync(follow);

            var notification = Assert.Single(await db.Notifications.ToListAsync());
            Assert.Equal("Follow", notification.Type);
            Assert.Equal("follow:25", notification.EventKey);
        }

        [Fact]
        public async Task CommentEvent_StoresCommentAndRecipeReferences()
        {
            await using var db = CreateDb();
            var recipe = NewRecipe();
            db.Users.AddRange(
                new ApplicationUser { Id = "owner" },
                new ApplicationUser { Id = "actor" });
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();

            var comment = new RecipeComment
            {
                Id = 30,
                UserId = "actor",
                RecipeId = recipe.Id,
                Recipe = recipe,
                Content = "Looks delicious"
            };
            db.RecipeComments.Add(comment);
            await db.SaveChangesAsync();

            await new NotificationService(db).NotifyRecipeCommentAsync(comment);

            var notification = Assert.Single(await db.Notifications.ToListAsync());
            Assert.Equal("Comment", notification.Type);
            Assert.Equal(recipe.Id, notification.RecipeId);
            Assert.Equal(comment.Id, notification.CommentId);
        }

        private static ApplicationDbContext CreateDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private static Recipe NewRecipe() => new()
        {
            Name = "Pho",
            UserId = "owner",
            Category = "Main",
            Description = "Recipe",
            Ingredients = "Ingredients",
            Instructions = "Instructions"
        };
    }
}
