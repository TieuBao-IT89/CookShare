using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Integration
{
    public sealed class AccountDeletionIntegrationTests : IAsyncLifetime
    {
        private const string Password = "DeletionPassword123!";
        private readonly AccountSettingsWebApplicationFactory _factory = new();

        public Task InitializeAsync() => _factory.InitializeAsync();

        public async Task DisposeAsync() => await _factory.DisposeAsync();

        [Fact]
        public async Task DeleteAccount_ShouldRemoveSessionOwnersGraphAndKeepUnrelatedData()
        {
            var graph = await SeedGraphAsync();
            using var client = _factory.CreateTestClient(useCookieAuthentication: true);
            await LoginAsync(client, graph.OwnerId);

            using var response = await DeleteAsync(client, Password, true, graph.OtherId);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/", response.Headers.Location?.OriginalString);
            Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
            Assert.Contains(cookies, cookie => cookie.StartsWith("CookShare.Auth=;", StringComparison.Ordinal));
            using var settingsAfterDeletion = await client.GetAsync("/Account/Settings");
            Assert.Equal(HttpStatusCode.Redirect, settingsAfterDeletion.StatusCode);
            Assert.Contains("/Account/Login", settingsAfterDeletion.Headers.Location?.OriginalString);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Users.AnyAsync(user => user.Id == graph.OwnerId));
            Assert.Equal(2, await db.Users.CountAsync());
            var other = await db.Users.SingleAsync(user => user.Id == graph.OtherId);
            Assert.Equal("Unrelated cook", other.DisplayName);
            Assert.Equal("/img/unrelated.webp", other.AvatarUrl);
            Assert.Equal("Keep this biography", other.Bio);
            Assert.Equal("/img/cover.webp", other.CoverImageUrl);
            Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .CheckPasswordAsync(other, Password));

            Assert.Equal(new[] { graph.OtherRecipeId, graph.SourceRecipeId },
                await db.Recipes.OrderBy(recipe => recipe.Id).Select(recipe => recipe.Id).ToArrayAsync());
            var imported = await db.Recipes.SingleAsync(recipe => recipe.Id == graph.SourceRecipeId);
            Assert.Null(imported.UserId);
            Assert.Equal("themealdb", imported.Source);
            Assert.Equal("account-deletion-source", imported.ExternalId);

            Assert.Equal(graph.OtherId, (await db.RecipeLikes.SingleAsync()).UserId);
            Assert.Equal(graph.OtherRecipeId, (await db.RecipeLikes.SingleAsync()).RecipeId);
            Assert.Equal(graph.KeptCommentId, (await db.RecipeComments.SingleAsync()).Id);
            Assert.Equal(graph.OtherId, (await db.Favorites.SingleAsync()).UserId);
            Assert.Equal(graph.OtherRecipeId, (await db.Favorites.SingleAsync()).RecipeId);
            Assert.Equal(graph.OtherId, (await db.RecipeRatings.SingleAsync()).UserId);
            Assert.Equal(5, (await db.RecipeRatings.SingleAsync()).Stars);
            var follow = await db.Follows.SingleAsync();
            Assert.Equal(graph.OtherId, follow.FollowerId);
            Assert.Equal(graph.ThirdId, follow.FollowingId);
            var pin = await db.ProfileFeaturedRecipes.SingleAsync();
            Assert.Equal(graph.OtherId, pin.UserId);
            Assert.Equal(graph.OtherRecipeId, pin.RecipeId);

            var notifications = await db.Notifications.OrderBy(item => item.EventKey).ToListAsync();
            Assert.Equal(new[] { "keep-comment-history", "keep-unrelated" },
                notifications.Select(item => item.EventKey));
            // Preserve PR #105's policy: a notification between surviving users
            // remains when only its deleted comment link becomes null.
            var history = notifications.Single(item => item.EventKey == "keep-comment-history");
            Assert.Null(history.CommentId);
            Assert.Equal(graph.OtherRecipeId, history.RecipeId);
            Assert.Equal(graph.KeptCommentId,
                notifications.Single(item => item.EventKey == "keep-unrelated").CommentId);
            Assert.All(notifications, item =>
            {
                Assert.Equal(graph.OtherId, item.RecipientId);
                Assert.Equal(graph.ThirdId, item.ActorId);
            });

            Assert.Equal(graph.OtherId, (await db.UserClaims.SingleAsync()).UserId);
            Assert.Equal(graph.OtherId, (await db.UserLogins.SingleAsync()).UserId);
            Assert.Equal(graph.OtherId, (await db.UserTokens.SingleAsync()).UserId);
            Assert.Equal(graph.OtherId, (await db.UserRoles.SingleAsync()).UserId);
            Assert.Single(await db.Roles.ToListAsync());
        }

        [Theory]
        [InlineData("IncorrectPassword123!", true)]
        [InlineData(Password, false)]
        [InlineData("", true)]
        public async Task DeleteAccount_ShouldRequirePasswordAndExplicitConfirmation(string password, bool confirm)
        {
            var graph = await SeedGraphAsync();
            var before = await CountsAsync();
            using var client = _factory.CreateTestClient(graph.OwnerId);

            using var response = await DeleteAsync(client, password, confirm, graph.OtherId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(before, await CountsAsync());
        }

        [Fact]
        public async Task DeleteAccount_ShouldRequireAuthenticationPostAndAntiforgery()
        {
            var graph = await SeedGraphAsync();
            var before = await CountsAsync();
            using var guest = _factory.CreateTestClient();
            using var unauthenticated = await guest.PostAsync("/Account/DeleteAccount",
                new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.Redirect, unauthenticated.StatusCode);
            Assert.Contains("/Account/Login", unauthenticated.Headers.Location?.OriginalString);

            using var client = _factory.CreateTestClient(graph.OwnerId);
            using var get = await client.GetAsync("/Account/DeleteAccount");
            Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
            using var missingToken = await client.PostAsync("/Account/DeleteAccount",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["DeleteForm.Password"] = Password,
                    ["DeleteForm.Confirm"] = "true"
                }));
            Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
            Assert.Equal(before, await CountsAsync());
        }

        [Fact]
        public async Task IdentityConcurrencyFailure_ShouldRollBackEveryDependentDeletion()
        {
            var graph = await SeedGraphAsync();
            var before = await CountsAsync();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByIdAsync(graph.OwnerId);
                Assert.NotNull(user);

                // Simulate a concurrent Identity update after this request read
                // the user. The real store returns ConcurrencyFailure on delete.
                await db.Users.Where(item => item.Id == graph.OwnerId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ConcurrencyStamp, "concurrent-update"));
                var result = await scope.ServiceProvider.GetRequiredService<AccountDeletionService>().DeleteAsync(user);

                Assert.False(result.Succeeded);
                Assert.Contains(result.Errors, error => error.Code == "ConcurrencyFailure");
            }

            Assert.Equal(before, await CountsAsync());
            using var verify = _factory.Services.CreateScope();
            var context = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal("concurrent-update", (await context.Users.SingleAsync(user => user.Id == graph.OwnerId)).ConcurrencyStamp);
            Assert.NotNull(await context.RecipeComments.SingleOrDefaultAsync(comment => comment.Id == graph.DeletedCommentId));
            Assert.Equal(graph.DeletedCommentId,
                (await context.Notifications.SingleAsync(item => item.EventKey == "keep-comment-history")).CommentId);
            Assert.Equal(3, await context.ProfileFeaturedRecipes.CountAsync());
        }

        private async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string password, bool confirm, string spoofedUserId)
        {
            string html = await client.GetStringAsync("/Account/Settings");
            string token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.False(string.IsNullOrEmpty(token));
            return await client.PostAsync("/Account/DeleteAccount?userId=" + Uri.EscapeDataString(spoofedUserId),
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token),
                    ["DeleteForm.Password"] = password,
                    ["DeleteForm.Confirm"] = confirm ? "true" : "false",
                    ["DeleteForm.UserId"] = spoofedUserId,
                    ["UserId"] = spoofedUserId,
                    ["Id"] = spoofedUserId
                }));
        }

        private async Task LoginAsync(HttpClient client, string userId)
        {
            using var scope = _factory.Services.CreateScope();
            var user = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(item => item.Id == userId);
            string html = await client.GetStringAsync("/Account/Login");
            string token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.False(string.IsNullOrEmpty(token));
            using var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token),
                ["UserNameOrEmail"] = user.UserName!,
                ["Password"] = Password
            }));
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        }

        private async Task<DeletionGraph> SeedGraphAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var owner = await CreateUserAsync(users, "Deleted cook");
            var other = await CreateUserAsync(users, "Unrelated cook");
            var third = await CreateUserAsync(users, "Another unrelated cook");
            other.AvatarUrl = "/img/unrelated.webp";
            other.Bio = "Keep this biography";
            other.CoverImageUrl = "/img/cover.webp";
            Assert.True((await users.UpdateAsync(other)).Succeeded);

            var ownedRecipe = RecipeFor(owner.Id, "Deleted recipe");
            var otherRecipe = RecipeFor(other.Id, "Kept recipe");
            var sourceRecipe = RecipeFor(null, "Imported recipe");
            sourceRecipe.Source = "themealdb";
            sourceRecipe.ExternalId = "account-deletion-source";
            sourceRecipe.SourceUrl = "https://www.themealdb.com/meal/account-deletion-source";
            db.Recipes.AddRange(ownedRecipe, otherRecipe, sourceRecipe);
            await db.SaveChangesAsync();

            var deletedComment = new RecipeComment { UserId = owner.Id, RecipeId = otherRecipe.Id, Content = "Remove my comment" };
            var keptComment = new RecipeComment { UserId = other.Id, RecipeId = otherRecipe.Id, Content = "Keep my comment" };
            db.RecipeComments.AddRange(deletedComment, keptComment,
                new RecipeComment { UserId = other.Id, RecipeId = ownedRecipe.Id, Content = "Deleted with the recipe" });
            db.RecipeLikes.AddRange(
                new RecipeLike { UserId = owner.Id, RecipeId = otherRecipe.Id },
                new RecipeLike { UserId = other.Id, RecipeId = ownedRecipe.Id },
                new RecipeLike { UserId = other.Id, RecipeId = otherRecipe.Id });
            db.Favorites.AddRange(
                new Favorite { UserId = owner.Id, RecipeId = otherRecipe.Id },
                new Favorite { UserId = other.Id, RecipeId = ownedRecipe.Id },
                new Favorite { UserId = other.Id, RecipeId = otherRecipe.Id });
            db.RecipeRatings.AddRange(
                new RecipeRating { UserId = owner.Id, RecipeId = otherRecipe.Id, Stars = 3 },
                new RecipeRating { UserId = other.Id, RecipeId = ownedRecipe.Id, Stars = 4 },
                new RecipeRating { UserId = other.Id, RecipeId = otherRecipe.Id, Stars = 5 });
            db.Follows.AddRange(
                new Follow { FollowerId = owner.Id, FollowingId = other.Id },
                new Follow { FollowerId = other.Id, FollowingId = owner.Id },
                new Follow { FollowerId = other.Id, FollowingId = third.Id });
            db.ProfileFeaturedRecipes.AddRange(
                new ProfileFeaturedRecipe { UserId = owner.Id, RecipeId = ownedRecipe.Id, DisplayOrder = 1 },
                new ProfileFeaturedRecipe { UserId = other.Id, RecipeId = otherRecipe.Id, DisplayOrder = 1 },
                // Exercise the recipe FK too, even if an old inconsistent pin
                // was created before the ownership validation was introduced.
                new ProfileFeaturedRecipe { UserId = third.Id, RecipeId = ownedRecipe.Id, DisplayOrder = 1 });

            var role = new IdentityRole("AccountDeletionTestRole");
            db.Roles.Add(role);
            foreach (string userId in new[] { owner.Id, other.Id })
            {
                db.UserClaims.Add(new IdentityUserClaim<string> { UserId = userId, ClaimType = ClaimTypes.Country, ClaimValue = "VN" });
                db.UserLogins.Add(new IdentityUserLogin<string> { UserId = userId, LoginProvider = "test", ProviderKey = userId });
                db.UserTokens.Add(new IdentityUserToken<string> { UserId = userId, LoginProvider = "test", Name = "opaque", Value = "disposable-test-value" });
                db.UserRoles.Add(new IdentityUserRole<string> { UserId = userId, RoleId = role.Id });
            }

            await db.SaveChangesAsync();
            db.Notifications.AddRange(
                Notice("remove-actor", owner.Id, other.Id, otherRecipe.Id),
                Notice("remove-recipient", other.Id, owner.Id, otherRecipe.Id),
                Notice("remove-owned-recipe", third.Id, other.Id, ownedRecipe.Id),
                Notice("keep-unrelated", third.Id, other.Id, otherRecipe.Id, keptComment.Id),
                Notice("keep-comment-history", third.Id, other.Id, otherRecipe.Id, deletedComment.Id));
            await db.SaveChangesAsync();
            return new DeletionGraph(owner.Id, other.Id, third.Id, otherRecipe.Id, sourceRecipe.Id, deletedComment.Id, keptComment.Id);
        }

        private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> users, string displayName)
        {
            string suffix = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            var user = new ApplicationUser { UserName = "delete_" + suffix, Email = suffix + "@example.com", DisplayName = displayName };
            var result = await users.CreateAsync(user, Password);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
            return user;
        }

        private static Recipe RecipeFor(string? userId, string name) => new()
        {
            UserId = userId,
            Name = name,
            Description = "Test recipe",
            Category = "Món Việt",
            Ingredients = "Rice",
            Instructions = "Cook"
        };

        private static Notification Notice(string key, string actor, string recipient, int recipeId, int? commentId = null) => new()
        {
            EventKey = key,
            ActorId = actor,
            RecipientId = recipient,
            RecipeId = recipeId,
            CommentId = commentId,
            Type = "Comment",
            Message = key
        };

        private async Task<int[]> CountsAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return new[]
            {
                await db.Users.CountAsync(), await db.Recipes.CountAsync(), await db.RecipeLikes.CountAsync(),
                await db.RecipeComments.CountAsync(), await db.Favorites.CountAsync(), await db.RecipeRatings.CountAsync(),
                await db.Follows.CountAsync(), await db.Notifications.CountAsync(), await db.ProfileFeaturedRecipes.CountAsync(),
                await db.UserClaims.CountAsync(), await db.UserLogins.CountAsync(), await db.UserTokens.CountAsync(),
                await db.UserRoles.CountAsync(), await db.Roles.CountAsync()
            };
        }

        private sealed record DeletionGraph(string OwnerId, string OtherId, string ThirdId,
            int OtherRecipeId, int SourceRecipeId, int DeletedCommentId, int KeptCommentId);
    }
}
