using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Integration
{
    public sealed class RecipeFavoritesIntegrationTests : IAsyncLifetime
    {
        private readonly IsolatedRecipeWebApplicationFactory _factory = new();

        public Task InitializeAsync()
        {
            return _factory.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            await _factory.DisposeAsync();
        }

        [Fact]
        public async Task SaveAndUnsave_ShouldPersistAcrossClientsAndIsolateAccountsDespiteSpoofedIdentifiers()
        {
            int recipeId = await SeedRecipeAsync("Persistent saved recipe");
            int secondId = await SeedRecipeAsync("Second persistent saved recipe");
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var other = _factory.CreateTestClient(_factory.OtherUserId);
            var spoof = new Dictionary<string, string>
            {
                ["UserId"] = _factory.OtherUserId,
                ["RecipeId"] = secondId.ToString(CultureInfo.InvariantCulture),
                ["CreatedAt"] = "2001-01-01"
            };

            for (int attempt = 0; attempt < 3; attempt++)
            {
                using var response = await PostFormAsync(owner, $"/Recipe/{recipeId}/Favorites", spoof);
                await AssertMutationAsync(response, recipeId, true, 1);
            }

            var stored = Assert.Single(await ReadFavoritesAsync());
            Assert.Equal(_factory.OwnerId, stored.UserId);
            Assert.Equal(recipeId, stored.RecipeId);
            Assert.True(stored.Id > 0);
            Assert.True(stored.CreatedAt.Year > 2001);
            using var refreshed = _factory.CreateTestClient(_factory.OwnerId);
            await AssertStateAsync(refreshed, new[] { recipeId, secondId }, new[] { recipeId }, 1);
            await AssertStateAsync(other, new[] { recipeId }, Array.Empty<int>(), 0);

            using var forged = await PostFormAsync(other, $"/Recipe/{recipeId}/Favorites/Unsave",
                new Dictionary<string, string> { ["UserId"] = _factory.OwnerId });
            await AssertMutationAsync(forged, recipeId, false, 0);
            Assert.Equal(_factory.OwnerId, Assert.Single(await ReadFavoritesAsync()).UserId);
            using var independentlySaved = await PostFormAsync(other, $"/Recipe/{recipeId}/Favorites");
            await AssertMutationAsync(independentlySaved, recipeId, true, 1);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var response = await PostFormAsync(owner, $"/Recipe/{recipeId}/Favorites/Unsave");
                await AssertMutationAsync(response, recipeId, false, 0);
            }

            Assert.Equal(_factory.OtherUserId, Assert.Single(await ReadFavoritesAsync()).UserId);
            await AssertStateAsync(refreshed, new[] { recipeId }, Array.Empty<int>(), 0);
            await AssertStateAsync(other, new[] { recipeId }, new[] { recipeId }, 1);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(2, await db.Recipes.CountAsync());
            Assert.Empty(await db.RecipeLikes.ToListAsync());
        }

        [Theory]
        [InlineData("/Recipe/Saved", "GET")]
        [InlineData("/Recipe/Favorites/State?recipeIds=1", "GET")]
        [InlineData("/Recipe/1/Favorites", "POST")]
        [InlineData("/Recipe/1/Favorites/Unsave", "POST")]
        public async Task AnonymousFavorites_ShouldRequireLogin(string path, string method)
        {
            using var client = _factory.CreateTestClient();
            using var request = new HttpRequestMessage(new HttpMethod(method), path);

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString ?? "");
            Assert.Empty(await ReadFavoritesAsync());
        }

        [Theory]
        [InlineData("")]
        [InlineData("/Unsave")]
        public async Task MissingCsrfToken_ShouldRejectBothMutations(string suffix)
        {
            int recipeId = await SeedRecipeAsync("Protected recipe");
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var response = await owner.PostAsync($"/Recipe/{recipeId}/Favorites{suffix}",
                new FormUrlEncodedContent(new Dictionary<string, string>()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(await ReadFavoritesAsync());
        }

        [Theory]
        [InlineData("")]
        [InlineData("/Unsave")]
        public async Task MissingRecipe_ShouldReturn404ForBothMutations(string suffix)
        {
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var response = await PostFormAsync(owner, $"/Recipe/2147483647/Favorites{suffix}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Empty(await ReadFavoritesAsync());
        }

        [Fact]
        public async Task State_ShouldExposeOnlyRequestedOwnIdsAndAccurateTotalWithNoStore()
        {
            int first = await SeedRecipeAsync("First saved");
            int second = await SeedRecipeAsync("Second saved");
            int otherRecipe = await SeedRecipeAsync("Other user only");
            await SeedFavoriteAsync(first, _factory.OwnerId);
            await SeedFavoriteAsync(second, _factory.OwnerId);
            await SeedFavoriteAsync(otherRecipe, _factory.OtherUserId);
            using var owner = _factory.CreateTestClient(_factory.OwnerId);

            await AssertStateAsync(owner, new[] { first, first, otherRecipe, 2147483647 }, new[] { first }, 2);
            await AssertStateAsync(owner, Array.Empty<int>(), Array.Empty<int>(), 2);
        }

        [Fact]
        public async Task SavedPage_ShouldRenderDatabaseRecipesNewestFirstEncodeTextAndKeepPrivateFavoritesOnOwnProfile()
        {
            const string unsafeName = "<script>alert('saved')</script>";
            const string unsafeDescription = "<img src=x onerror=alert(1)>";
            int first = await SeedRecipeAsync("Older saved favorite", _factory.OtherUserId);
            int second = await SeedRecipeAsync(unsafeName, _factory.OtherUserId, unsafeDescription);
            int unrelated = await SeedRecipeAsync("Unrelated unsaved recipe", _factory.OtherUserId);
            await SeedFavoriteAsync(first, _factory.OwnerId, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            await SeedFavoriteAsync(second, _factory.OwnerId, new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            await SeedFavoriteAsync(unrelated, _factory.OtherUserId);
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var other = _factory.CreateTestClient(_factory.OtherUserId);
            using var anonymous = _factory.CreateTestClient();

            using var page = await owner.GetAsync("/Recipe/Saved");
            page.EnsureSuccessStatusCode();
            string html = await page.Content.ReadAsStringAsync();

            Assert.True(page.Headers.CacheControl?.NoStore);
            Assert.Equal(new[] { second, first }, GetCardIds(html));
            Assert.Contains($"href=\"/Recipe/Details/{first}\"", html);
            Assert.Contains($"href=\"/Recipe/Details/{second}\"", html);
            Assert.DoesNotContain("Unrelated unsaved recipe", html);
            Assert.DoesNotContain(unsafeName, html);
            Assert.DoesNotContain(unsafeDescription, html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
            Assert.Matches("id=\"savedRecipeCount\"[^>]*>\\s*2\\s*<", html);
            Assert.Contains("recipe-saved.css", html);
            Assert.Contains("saved-card-unsave-btn", html);
            Assert.DoesNotContain("src=\"/js/recipe.js", html);

            string ownProfile = await owner.GetStringAsync("/Profile");
            Assert.Contains("id=\"favorites\"", ownProfile);
            Assert.Equal(new[] { second, first }, GetCardIds(GetFavoritesSection(ownProfile)));
            foreach (var client in new[] { other, anonymous })
            {
                string privateProfile = await client.GetStringAsync("/Profile?userId=" + _factory.OwnerId);
                Assert.DoesNotContain("id=\"favorites\"", privateProfile);
                Assert.DoesNotContain("Older saved favorite", privateProfile);
                Assert.DoesNotContain("&lt;script&gt;", privateProfile);
            }

            string othersSaved = await other.GetStringAsync("/Recipe/Saved");
            Assert.Equal(new[] { unrelated }, GetCardIds(othersSaved));
        }

        [Fact]
        public async Task EmptySavedPage_ShouldKeepFriendlyEmptyStateAndZeroCount()
        {
            await SeedRecipeAsync("Not a favorite");
            using var owner = _factory.CreateTestClient(_factory.OwnerId);

            string html = await owner.GetStringAsync("/Recipe/Saved");

            Assert.Empty(GetCardIds(html));
            Assert.Contains("id=\"savedEmptyState\"", html);
            Assert.Matches("id=\"savedRecipeCount\"[^>]*>\\s*0\\s*<", html);
            Assert.DoesNotContain("Not a favorite", html);
        }

        [Fact]
        public async Task PublishedPages_ShouldInitializeEverySaveButtonFromAccountFavorites()
        {
            int recipeId = await SeedRecipeAsync("Saved button consistency");
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var other = _factory.CreateTestClient(_factory.OtherUserId);
            using var saved = await PostFormAsync(owner, $"/Recipe/{recipeId}/Favorites");
            saved.EnsureSuccessStatusCode();
            foreach (string path in new[] { "/", "/Recipe", $"/Recipe/Details/{recipeId}", "/Recipe/Saved", "/Profile" })
            {
                string html = await owner.GetStringAsync(path);
                AssertSaveButtons(html, recipeId, true);
            }

            foreach (string path in new[] { "/", "/Recipe", $"/Recipe/Details/{recipeId}", "/Profile?userId=" + _factory.OwnerId })
            {
                string html = await other.GetStringAsync(path);
                AssertSaveButtons(html, recipeId, false);
            }

            using var unsaved = await PostFormAsync(owner, $"/Recipe/{recipeId}/Favorites/Unsave");
            unsaved.EnsureSuccessStatusCode();
            foreach (string path in new[] { "/", "/Recipe", $"/Recipe/Details/{recipeId}", "/Profile" })
            {
                AssertSaveButtons(await owner.GetStringAsync(path), recipeId, false);
            }
        }

        [Fact]
        public async Task RealIdentityRegisterLogoutLogin_ShouldRestoreSavedRecipesAcrossNewBrowserSession()
        {
            await using var cookieFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultForbidScheme = IdentityConstants.ApplicationScheme;
                })));
            int recipeId;
            using (var scope = cookieFactory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Database.EnsureCreatedAsync();
                var recipe = NewRecipe("Cookie persistence recipe", null);
                db.Recipes.Add(recipe);
                await db.SaveChangesAsync();
                recipeId = recipe.Id;
            }

            var clientOptions = new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
                BaseAddress = new Uri("https://localhost")
            };
            using var client = cookieFactory.CreateClient(clientOptions);
            const string email = "saved-cookie@example.com";
            const string password = "TestPassword123!";
            using var registered = await PostFormAsync(client, "/Account/Register", new Dictionary<string, string>
            {
                ["FirstName"] = "Saved",
                ["LastName"] = "Cookie",
                ["UserName"] = "saved-cookie-cook",
                ["Email"] = email,
                ["Password"] = password,
                ["ConfirmPassword"] = password
            }, "/Account/Register");
            Assert.Equal(HttpStatusCode.Redirect, registered.StatusCode);
            Assert.Contains(registered.Headers.GetValues("Set-Cookie"), value => value.StartsWith("CookShare.Auth=", StringComparison.Ordinal));
            using var saved = await PostFormAsync(client, $"/Recipe/{recipeId}/Favorites");
            await AssertMutationAsync(saved, recipeId, true, 1);
            string userId = Assert.Single(await ReadFavoritesAsync(cookieFactory.Services)).UserId;
            using var logout = await PostFormAsync(client, "/Account/Logout");
            Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
            using var privatePage = await client.GetAsync("/Recipe/Saved");
            Assert.Equal(HttpStatusCode.Redirect, privatePage.StatusCode);

            using var newBrowser = cookieFactory.CreateClient(clientOptions);
            using var login = await PostFormAsync(newBrowser, "/Account/Login?returnUrl=%2FRecipe%2FSaved", new Dictionary<string, string>
            {
                ["UserNameOrEmail"] = email,
                ["Password"] = password
            }, "/Account/Login");
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            Assert.Equal("/Recipe/Saved", login.Headers.Location?.OriginalString);
            await AssertStateAsync(newBrowser, new[] { recipeId }, new[] { recipeId }, 1);
            Assert.Equal(new[] { recipeId }, GetCardIds(await newBrowser.GetStringAsync("/Recipe/Saved")));
            Assert.Equal(userId, Assert.Single(await ReadFavoritesAsync(cookieFactory.Services)).UserId);
        }

        private static void AssertSaveButtons(string html, int recipeId, bool saved)
        {
            var buttons = Regex.Matches(html, @"<button\b[^>]*\bdata-db-recipe-save\b[^>]*>", RegexOptions.IgnoreCase)
                .Select(match => match.Value)
                .Where(button => button.Contains($"data-recipe-id=\"{recipeId}\"", StringComparison.Ordinal)).ToArray();
            Assert.NotEmpty(buttons);
            Assert.All(buttons, button =>
            {
                Assert.Contains("recipe-save-btn", button);
                Assert.Contains($"aria-pressed=\"{saved.ToString().ToLowerInvariant()}\"", button);
                Assert.Contains($"data-is-saved=\"{saved.ToString().ToLowerInvariant()}\"", button);
            });
        }

        private async Task<int> SeedRecipeAsync(string name, string? userId = null, string description = "Database favorite description")
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var recipe = NewRecipe(name, userId ?? _factory.OwnerId, description);
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            return recipe.Id;
        }

        private static Recipe NewRecipe(string name, string? userId, string description = "Database favorite description")
        {
            return new Recipe
            {
                Name = name,
                Description = description,
                UserId = userId,
                Category = "Dinner",
                Ingredients = "Fresh ingredients",
                Instructions = "Mix and cook"
            };
        }

        private async Task SeedFavoriteAsync(int recipeId, string userId, DateTime? timestamp = null)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Favorites.Add(new Favorite { RecipeId = recipeId, UserId = userId, CreatedAt = timestamp ?? DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        private async Task<List<Favorite>> ReadFavoritesAsync(IServiceProvider? services = null)
        {
            using var scope = (services ?? _factory.Services).CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Favorites.AsNoTracking().ToListAsync();
        }

        private static async Task AssertMutationAsync(HttpResponseMessage response, int recipeId, bool saved, int count)
        {
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(recipeId, json.GetProperty("recipeId").GetInt32());
            Assert.Equal(saved, json.GetProperty("isSaved").GetBoolean());
            Assert.Equal(count, json.GetProperty("savedCount").GetInt32());
            Assert.False(string.IsNullOrEmpty(json.GetProperty("message").GetString()));
            Assert.True(response.Headers.CacheControl?.NoStore);
        }

        private static async Task AssertStateAsync(HttpClient client, int[] requested, int[] saved, int count)
        {
            string query = string.Join("&", requested.Select(id => "recipeIds=" + id.ToString(CultureInfo.InvariantCulture)));
            using var response = await client.GetAsync("/Recipe/Favorites/State?" + query);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(saved.OrderBy(id => id), json.GetProperty("savedRecipeIds").EnumerateArray()
                .Select(id => id.GetInt32()).OrderBy(id => id));
            Assert.Equal(count, json.GetProperty("savedCount").GetInt32());
            Assert.False(string.IsNullOrEmpty(json.GetProperty("userId").GetString()));
            Assert.True(response.Headers.CacheControl?.NoStore);
        }

        private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url,
            Dictionary<string, string>? form = null, string tokenPage = "/Recipe/Create")
        {
            using var page = await client.GetAsync(tokenPage);
            page.EnsureSuccessStatusCode();
            string html = await page.Content.ReadAsStringAsync();
            string input = Regex.Matches(html, @"<input\b[^>]*>", RegexOptions.IgnoreCase)
                .Select(match => match.Value)
                .First(tag => tag.Contains("name=\"__RequestVerificationToken\"", StringComparison.Ordinal));
            var match = Regex.Match(input, "value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
            Assert.True(match.Success);
            var values = form == null ? new Dictionary<string, string>() : new Dictionary<string, string>(form);
            values["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value);
            return await client.PostAsync(url, new FormUrlEncodedContent(values));
        }

        private static int[] GetCardIds(string html)
        {
            return Regex.Matches(html, "<article\\b[^>]*\\bdata-recipe-id=\"(\\d+)\"", RegexOptions.IgnoreCase)
                .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
        }

        private static string GetFavoritesSection(string html)
        {
            return Regex.Matches(html, @"<section\b[^>]*>[\s\S]*?</section>", RegexOptions.IgnoreCase)
                .Select(match => match.Value).Single(section => section.Contains("id=\"favorites\"", StringComparison.Ordinal));
        }
    }
}
