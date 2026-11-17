using System.Security.Claims;
using CookShare.Controllers;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Integration
{
    [Collection(PostgreSqlIntegrationCollection.Name)]
    public class NotificationTransactionIntegrationTests
    {
        private readonly CookShareWebApplicationFactory _factory;

        public NotificationTransactionIntegrationTests(
            CookShareWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [PostgresTheory]
        [InlineData("Follow")]
        [InlineData("Like")]
        [InlineData("Comment")]
        [InlineData("AjaxLike")]
        [InlineData("AjaxComment")]
        public async Task NotificationFailure_RollsBackInteractionAndAllowsRetry(
            string interaction)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var actorId = "actor-" + suffix;
            var ownerId = "owner-" + suffix;
            int recipeId;

            await using (var db = CreateDb())
            {
                db.Users.AddRange(
                    new ApplicationUser { Id = actorId, UserName = actorId },
                    new ApplicationUser { Id = ownerId, UserName = ownerId });
                var recipe = new Recipe
                {
                    UserId = ownerId,
                    Name = "Rollback test",
                    Category = "Main",
                    Description = "Recipe",
                    Ingredients = "Ingredients",
                    Instructions = "Instructions"
                };
                db.Recipes.Add(recipe);
                await db.SaveChangesAsync();
                recipeId = recipe.Id;
            }

            var interceptor = new RejectNotificationSaveInterceptor();
            await using (var db = CreateDb(interceptor))
            {
                if (interaction.StartsWith("Ajax", StringComparison.Ordinal))
                {
                    var failure = Assert.IsType<ObjectResult>(await InteractAsync(
                        db, interaction, actorId, ownerId, recipeId));
                    Assert.Equal(StatusCodes.Status503ServiceUnavailable, failure.StatusCode);
                }
                else
                {
                    await Assert.ThrowsAsync<DbUpdateException>(() =>
                        InteractAsync(db, interaction, actorId, ownerId, recipeId));
                }
                Assert.True(interceptor.RejectedNotification);
            }

            // A fresh connection verifies persisted state after transaction disposal.
            await using (var db = CreateDb())
            {
                Assert.Equal(0, await CountInteractionsAsync(
                    db, interaction, actorId));
                Assert.False(await db.Notifications.AnyAsync(
                    notification => notification.ActorId == actorId));
            }

            await using (var db = CreateDb())
            {
                IActionResult result = await InteractAsync(db, interaction, actorId, ownerId, recipeId);
                if (interaction.StartsWith("Ajax", StringComparison.Ordinal))
                {
                    Assert.IsType<JsonResult>(result);
                }
                else
                {
                    Assert.IsType<RedirectToActionResult>(result);
                }
            }

            await using (var db = CreateDb())
            {
                Assert.Equal(1, await CountInteractionsAsync(
                    db, interaction, actorId));
                var notification = await db.Notifications.SingleAsync(
                    item => item.ActorId == actorId);
                Assert.Equal(ownerId, notification.RecipientId);
                Assert.Equal(interaction.Replace("Ajax", string.Empty, StringComparison.Ordinal), notification.Type);
            }
        }

        private ApplicationDbContext CreateDb(
            SaveChangesInterceptor? interceptor = null)
        {
            using var scope = _factory.Services.CreateScope();
            var connection = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>()
                .Database.GetConnectionString();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connection);
            if (interceptor != null)
            {
                options.AddInterceptors(interceptor);
            }

            return new ApplicationDbContext(options.Options);
        }

        private static Task<IActionResult> InteractAsync(
            ApplicationDbContext db,
            string interaction,
            string actorId,
            string ownerId,
            int recipeId)
        {
            var context = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, actorId) },
                        "Test"))
                }
            };

            if (interaction == "Follow")
            {
                return new ProfileController(db, null!)
                {
                    ControllerContext = context
                }.Follow(ownerId);
            }

            if (interaction == "AjaxLike")
            {
                return new RecipeLikesController(db) { ControllerContext = context }.Like(recipeId);
            }

            if (interaction == "AjaxComment")
            {
                return new RecipeCommentsController(db) { ControllerContext = context }
                    .Create(recipeId, new CookShare.ViewModels.CommentFormViewModel { Content = "Looks delicious" });
            }

            var controller = new RecipeController(db)
            {
                ControllerContext = context
            };
            return interaction == "Like"
                ? controller.Like(recipeId)
                : controller.AddComment(recipeId, "Looks delicious");
        }

        private static Task<int> CountInteractionsAsync(
            ApplicationDbContext db,
            string interaction,
            string actorId) => interaction switch
            {
                "Follow" => db.Follows.CountAsync(item => item.FollowerId == actorId),
                "Like" or "AjaxLike" => db.RecipeLikes.CountAsync(item => item.UserId == actorId),
                _ => db.RecipeComments.CountAsync(item => item.UserId == actorId)
            };

        private sealed class RejectNotificationSaveInterceptor
            : SaveChangesInterceptor
        {
            public bool RejectedNotification { get; private set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                if (eventData.Context!.ChangeTracker.Entries<Notification>()
                    .Any(entry => entry.State == EntityState.Added))
                {
                    RejectedNotification = true;
                    throw new DbUpdateException("Simulated notification write failure.");
                }

                return ValueTask.FromResult(result);
            }
        }
    }
}
