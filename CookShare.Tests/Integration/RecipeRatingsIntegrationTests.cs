using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Integration
{
    public sealed class RecipeRatingsIntegrationTests : IAsyncLifetime
    {
        private readonly IsolatedRecipeWebApplicationFactory _factory = new();

        public Task InitializeAsync() => _factory.InitializeAsync();
        public async Task DisposeAsync() => await _factory.DisposeAsync();

        [Fact]
        public async Task SaveAndUpdate_ShouldKeepOneRatingPerAccountAndCalculateAverageFromAllUsers()
        {
            int recipeId = await SeedRecipeAsync();
            using var reader = _factory.CreateTestClient(_factory.OtherUserId);
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var guest = _factory.CreateTestClient();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Users.Add(new ApplicationUser { Id = "third-reader", UserName = "third-reader" });
                await db.SaveChangesAsync();
            }

            using var third = _factory.CreateTestClient("third-reader");
            await AssertStateAsync(guest, recipeId, 0, 0, null, false);
            using var first = await PostAsync(reader, recipeId, "5", extra: new Dictionary<string, string>
            {
                ["UserId"] = _factory.OwnerId,
                ["RecipeId"] = "2147483647",
                ["CreatedAt"] = "2001-01-01"
            });
            first.EnsureSuccessStatusCode();
            using var second = await PostAsync(third, recipeId, "2");
            second.EnsureSuccessStatusCode();
            await AssertStateAsync(reader, recipeId, 2, 3.5, 5, true);
            await AssertStateAsync(third, recipeId, 2, 3.5, 2, true);
            await AssertStateAsync(owner, recipeId, 2, 3.5, null, false);
            await AssertStateAsync(guest, recipeId, 2, 3.5, null, false);

            using var updated = await PostAsync(reader, recipeId, "4");
            updated.EnsureSuccessStatusCode();
            using var repeat = await PostAsync(reader, recipeId, "4");
            repeat.EnsureSuccessStatusCode();
            using var refreshed = _factory.CreateTestClient(_factory.OtherUserId);
            await AssertStateAsync(refreshed, recipeId, 2, 3, 4, true);
            var stored = await ReadRatingsAsync();
            Assert.Equal(2, stored.Count);
            var own = Assert.Single(stored, rating => rating.UserId == _factory.OtherUserId);
            Assert.Equal(recipeId, own.RecipeId);
            Assert.Equal(4, own.Stars);
            Assert.True(own.CreatedAt.Year > 2001);
            Assert.DoesNotContain(stored, rating => rating.UserId == _factory.OwnerId);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("6")]
        [InlineData("-1")]
        [InlineData("2.5")]
        [InlineData("abc")]
        [InlineData("")]
        public async Task InvalidStars_ShouldReturn400AndLeaveExistingRatingUnchanged(string stars)
        {
            int recipeId = await SeedRecipeAsync();
            using var client = _factory.CreateTestClient(_factory.OtherUserId);
            using var valid = await PostAsync(client, recipeId, "3");
            valid.EnsureSuccessStatusCode();
            using var invalid = await PostAsync(client, recipeId, stars);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal(3, Assert.Single(await ReadRatingsAsync()).Stars);
        }

        [Fact]
        public async Task AuthorAnonymousAndMissingCsrf_ShouldNotWriteRatings()
        {
            int recipeId = await SeedRecipeAsync();
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var reader = _factory.CreateTestClient(_factory.OtherUserId);
            using var guest = _factory.CreateTestClient();
            using var forbidden = await PostAsync(owner, recipeId, "5");
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            using var unauthorized = await guest.PostAsync($"/Recipe/{recipeId}/Ratings", new FormUrlEncodedContent(new Dictionary<string, string> { ["Stars"] = "5" }));
            Assert.Equal(HttpStatusCode.Redirect, unauthorized.StatusCode);
            Assert.Contains("/Account/Login", unauthorized.Headers.Location?.OriginalString ?? "");
            using var csrf = await reader.PostAsync($"/Recipe/{recipeId}/Ratings", new FormUrlEncodedContent(new Dictionary<string, string> { ["Stars"] = "5" }));
            Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
            Assert.Empty(await ReadRatingsAsync());
        }

        [Fact]
        public async Task MissingRecipe_ShouldReturn404ForReadAndWrite()
        {
            using var client = _factory.CreateTestClient(_factory.OtherUserId);
            using var get = await client.GetAsync("/Recipe/2147483647/Ratings");
            using var post = await PostAsync(client, int.MaxValue, "5");
            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
            Assert.Empty(await ReadRatingsAsync());
        }

        [Fact]
        public async Task NativeForm_ShouldSaveAndRedirectToRatingSectionWithoutJavaScript()
        {
            int recipeId = await SeedRecipeAsync();
            using var client = _factory.CreateTestClient(_factory.OtherUserId);
            using var response = await PostAsync(client, recipeId, "4", json: false);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal($"/Recipe/Details/{recipeId}#recipeRatingSection", response.Headers.Location?.OriginalString);
            Assert.Equal(4, Assert.Single(await ReadRatingsAsync()).Stars);
        }

        [Fact]
        public async Task Pages_ShouldRenderRealSummariesAndOnlyReadersGetTheRatingForm()
        {
            int recipeId = await SeedRecipeAsync();
            using var reader = _factory.CreateTestClient(_factory.OtherUserId);
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var guest = _factory.CreateTestClient();
            using var saved = await PostAsync(reader, recipeId, "4");
            saved.EnsureSuccessStatusCode();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Favorites.Add(new Favorite { UserId = _factory.OtherUserId, RecipeId = recipeId });
                await db.SaveChangesAsync();
            }

            foreach (string path in new[] { "/Recipe", $"/Recipe/Details/{recipeId}", "/Recipe/Saved", $"/Profile?userId={_factory.OwnerId}", "/" })
            {
                string html = await reader.GetStringAsync(path);
                Assert.Contains("data-db-rating-summary", html);
                Assert.Contains("data-average-rating=\"4\"", html);
                Assert.Contains("data-rating-count=\"1\"", html);
            }

            string details = await reader.GetStringAsync($"/Recipe/Details/{recipeId}");
            Assert.Contains("data-rating-form", details);
            Assert.Contains("name=\"Stars\"", details);
            Assert.Contains("__RequestVerificationToken", details);
            Assert.Contains("recipe-ratings.js", details);
            Assert.DoesNotContain("data-rating-form", await owner.GetStringAsync($"/Recipe/Details/{recipeId}"));
            string anonymous = await guest.GetStringAsync($"/Recipe/Details/{recipeId}");
            Assert.DoesNotContain("data-rating-form", anonymous);
            Assert.Contains("/Account/Login?returnUrl=", anonymous);
        }

        private async Task<int> SeedRecipeAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var recipe = new Recipe { UserId = _factory.OwnerId, Name = "Recipe with real ratings", Description = "A recipe", Category = "Dinner", Ingredients = "Rice", Instructions = "Cook" };
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            return recipe.Id;
        }

        private async Task<List<RecipeRating>> ReadRatingsAsync()
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RecipeRatings.AsNoTracking().ToListAsync();
        }

        private static async Task AssertStateAsync(HttpClient client, int id, int count, double average, int? own, bool canRate)
        {
            using var response = await client.GetAsync($"/Recipe/{id}/Ratings");
            response.EnsureSuccessStatusCode();
            Assert.True(response.Headers.CacheControl?.NoStore);
            var state = await response.Content.ReadFromJsonAsync<RecipeRatingViewModel>();
            Assert.NotNull(state);
            Assert.Equal(id, state.RecipeId);
            Assert.Equal(count, state.RatingCount);
            Assert.Equal(average, state.AverageRating, 8);
            Assert.Equal(own, state.UserRating);
            Assert.Equal(canRate, state.CanRate);
        }

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, int id, string stars, bool json = true, Dictionary<string, string>? extra = null)
        {
            string page = await client.GetStringAsync("/Recipe/Create");
            string token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.False(string.IsNullOrEmpty(token));
            var values = extra == null ? new Dictionary<string, string>() : new Dictionary<string, string>(extra);
            values["Stars"] = stars;
            values["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/Recipe/{id.ToString(CultureInfo.InvariantCulture)}/Ratings") { Content = new FormUrlEncodedContent(values) };
            if (json) request.Headers.Accept.ParseAdd("application/json");
            return await client.SendAsync(request);
        }
    }
}
