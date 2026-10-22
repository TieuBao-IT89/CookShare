using System.Net;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Integration
{
    [Collection(PostgreSqlIntegrationCollection.Name)]
    public class NotificationIntegrationTests
    {
        private readonly CookShareWebApplicationFactory _factory;

        public NotificationIntegrationTests(
            CookShareWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Like_CreatesPrivateNotificationAndOwnerCanReadIt()
        {
            var recipeId = await SeedRecipeAsync(_factory.OwnerId);
            using var actorClient =
                _factory.CreateTestClient(_factory.OtherUserId);

            var token = await GetTokenAsync(
                actorClient,
                $"/Recipe/Details/{recipeId}");
            using var likeResponse = await actorClient.PostAsync(
                $"/Recipe/Like/{recipeId}",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token
                }));

            Assert.Equal(HttpStatusCode.Redirect, likeResponse.StatusCode);

            using var commentResponse = await actorClient.PostAsync(
                $"/Recipe/AddComment/{recipeId}",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["content"] = "This looks delicious"
                }));
            Assert.Equal(HttpStatusCode.Redirect, commentResponse.StatusCode);

            var ownerProfileUrl =
                $"/Profile/Index?userId={Uri.EscapeDataString(_factory.OwnerId)}";
            var followToken = await GetTokenAsync(
                actorClient,
                ownerProfileUrl);
            using var followResponse = await actorClient.PostAsync(
                "/Profile/Follow",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = followToken,
                    ["userId"] = _factory.OwnerId
                }));
            Assert.Equal(HttpStatusCode.Redirect, followResponse.StatusCode);

            int likeNotificationId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();
                var notification = await db.Notifications
                    .AsNoTracking()
                    .SingleAsync(item =>
                        item.RecipeId == recipeId
                        && item.ActorId == _factory.OtherUserId
                        && item.Type == "Like");

                Assert.Equal(_factory.OwnerId, notification.RecipientId);
                likeNotificationId = notification.Id;
                Assert.Equal(
                    new[] { "Comment", "Follow", "Like" },
                    await db.Notifications
                        .AsNoTracking()
                        .Where(item =>
                            item.RecipientId == _factory.OwnerId
                            && item.ActorId == _factory.OtherUserId)
                        .Select(item => item.Type)
                        .OrderBy(type => type)
                        .ToArrayAsync());
            }

            using var ownerClient =
                _factory.CreateTestClient(_factory.OwnerId);
            using var ownerPage = await ownerClient.GetAsync(
                "/Notifications/Index");
            Assert.Equal(HttpStatusCode.OK, ownerPage.StatusCode);
            var notificationsHtml = WebUtility.HtmlDecode(
                await ownerPage.Content.ReadAsStringAsync());
            Assert.Contains(
                "đã thích công thức",
                notificationsHtml);

            var ownerToken = await GetTokenAsync(
                ownerClient,
                "/Notifications/Index");
            using var openResponse = await ownerClient.PostAsync(
                $"/Notifications/Open/{likeNotificationId}",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = ownerToken
                }));

            Assert.Equal(HttpStatusCode.Redirect, openResponse.StatusCode);
            Assert.Contains(
                $"/Recipe/Details/{recipeId}",
                openResponse.Headers.Location?.ToString() ?? "");

            var actorToken = await GetTokenAsync(
                actorClient,
                "/Recipe/Create");
            using var unauthorizedResponse = await actorClient.PostAsync(
                $"/Notifications/MarkAsRead/{likeNotificationId}",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = actorToken
                }));

            Assert.Equal(HttpStatusCode.NotFound, unauthorizedResponse.StatusCode);
        }

        private async Task<int> SeedRecipeAsync(string ownerId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            var recipe = new Recipe
            {
                UserId = ownerId,
                Name = "Notification test " + Guid.NewGuid(),
                Description = "Recipe",
                Category = "Main",
                Ingredients = "Ingredients",
                Instructions = "Instructions",
                CreatedAt = DateTime.UtcNow
            };

            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            return recipe.Id;
        }

        private static async Task<string> GetTokenAsync(
            HttpClient client,
            string url)
        {
            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            var input = Regex.Matches(
                    html,
                    @"<input\b[^>]*>",
                    RegexOptions.IgnoreCase)
                .Select(match => match.Value)
                .FirstOrDefault(tag => tag.Contains(
                    "name=\"__RequestVerificationToken\"",
                    StringComparison.OrdinalIgnoreCase));

            if (input == null)
            {
                throw new InvalidOperationException(
                    "Anti-forgery token not found in page.");
            }

            var match = Regex.Match(
                input,
                "value=\"([^\"]+)\"",
                RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                throw new InvalidOperationException(
                    "Anti-forgery token has no value.");
            }

            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }
    }
}
