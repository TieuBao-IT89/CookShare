using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Integration
{
    public sealed class RecipePagesIntegrationTests : IAsyncLifetime
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
        public async Task Home_ShouldRenderDatabaseFeaturedAndLatestWithStableOrdering()
        {
            var timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var first = await SeedRecipeAsync("First recipe", createdAt: timestamp);
            var second = await SeedRecipeAsync("Second recipe", createdAt: timestamp.AddDays(1));
            var third = await SeedRecipeAsync("Third recipe", createdAt: timestamp.AddDays(2));
            var fourth = await SeedRecipeAsync("Fourth recipe", createdAt: timestamp.AddDays(3));
            var fifth = await SeedRecipeAsync("Fifth recipe", createdAt: timestamp.AddDays(3));
            using var client = _factory.CreateTestClient();

            var html = await GetHtmlAsync(client, "/");
            var featured = GetSectionContaining(html, "homeFeaturedGrid");
            var popular = GetSectionContaining(html, "homePopularGrid");
            var topRated = GetSectionContaining(html, "homeTopRatedGrid");
            var latest = GetSectionContaining(html, "homeFeedItems");

            Assert.Empty(GetCardIds(featured));
            Assert.Contains("Chưa có món nổi bật", featured);
            Assert.Empty(GetCardIds(popular));
            Assert.Contains("Chưa có công thức nào được yêu thích.", popular);
            Assert.Empty(GetCardIds(topRated));
            Assert.Contains("Chưa có công thức nào được đánh giá.", topRated);
            Assert.Equal(new[] { fifth, fourth, third, second, first }, GetCardIds(latest));
            AssertRecipeLink(latest, fifth, "Fifth recipe");
            Assert.Contains("Kitchen Owner", latest);
            Assert.Contains("/Profile?userId=" + _factory.OwnerId, latest);
            Assert.DoesNotContain("Fourth recipe", featured);
            Assert.Contains("Second recipe", latest);
        }

        [Fact]
        public async Task Home_ShouldRankFeaturedPopularAndTopRatedFromLikesAndRatings()
        {
            var timestamp = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var quiet = await SeedRecipeAsync("Quiet soup", createdAt: timestamp);
            var liked = await SeedRecipeAsync("Liked noodles", createdAt: timestamp.AddDays(1));
            var loved = await SeedRecipeAsync("Loved curry", createdAt: timestamp.AddDays(2));
            var rated = await SeedRecipeAsync("Rated salad", createdAt: timestamp.AddDays(3));
            var praised = await SeedRecipeAsync("Praised cake", createdAt: timestamp.AddDays(4));
            await SeedLikeAsync(loved, _factory.OwnerId);
            await SeedLikeAsync(loved, _factory.OtherUserId);
            await SeedLikeAsync(loved, "fan-3");
            await SeedLikeAsync(liked, _factory.OwnerId);
            await SeedRatingAsync(praised, _factory.OwnerId, 5);
            await SeedRatingAsync(praised, _factory.OtherUserId, 5);
            await SeedRatingAsync(rated, _factory.OwnerId, 4);
            using var client = _factory.CreateTestClient();

            var html = await GetHtmlAsync(client, "/");
            var featured = GetSectionContaining(html, "homeFeaturedGrid");
            var popular = GetSectionContaining(html, "homePopularGrid");
            var topRated = GetSectionContaining(html, "homeTopRatedGrid");

            Assert.Equal(3, RecipeDiscoveryQueries.SectionSize);
            Assert.Equal(new[] { praised, loved, rated }, GetDiscoveryIds(featured));
            Assert.Equal(new[] { loved, liked }, GetDiscoveryIds(popular));
            Assert.Equal(new[] { praised, rated }, GetDiscoveryIds(topRated));
            Assert.DoesNotContain($"data-recipe-id=\"{quiet}\"", featured);
            Assert.DoesNotContain($"data-recipe-id=\"{quiet}\"", popular);
            Assert.DoesNotContain($"data-recipe-id=\"{quiet}\"", topRated);
        }

        [Fact]
        public async Task Details_ShouldShowRelatedRecipesInTheSameCategoryWithoutTheCurrentRecipe()
        {
            var current = await SeedRecipeAsync("Pho", category: "Món Việt");
            var sibling = await SeedRecipeAsync("Bun", category: "món việt");
            var plain = await SeedRecipeAsync("Rolls", category: "Món Việt");
            var other = await SeedRecipeAsync("Cake", category: "Món tráng miệng");
            await SeedLikeAsync(sibling, _factory.OwnerId);
            using var client = _factory.CreateTestClient();

            var html = await GetHtmlAsync(client, $"/Recipe/Details/{current}");
            var related = GetSectionContaining(html, "relatedRecipes");

            Assert.Equal(new[] { sibling, plain }, GetCardIds(related));
            Assert.DoesNotContain($"data-recipe-id=\"{current}\"", related);
            Assert.DoesNotContain($"data-recipe-id=\"{other}\"", related);
            Assert.Contains("Bun", related);
            Assert.DoesNotContain("Cake", related);

            var only = await GetHtmlAsync(client, $"/Recipe/Details/{other}");
            var emptyRelated = GetSectionContaining(only, "relatedRecipes");
            Assert.Empty(GetCardIds(emptyRelated));
            Assert.Contains("Chưa có công thức liên quan trong danh mục này.", emptyRelated);
        }

        [Fact]
        public async Task EmptyHomeAndExplore_ShouldShowEmptyStatesWithoutDemoRecipeLinks()
        {
            using var client = _factory.CreateTestClient();

            var home = await GetHtmlAsync(client, "/");
            var explore = await GetHtmlAsync(client, "/Recipe");

            Assert.Contains("Chưa có công thức nào được chia sẻ.", home);
            Assert.Empty(GetCardIds(home));
            Assert.Empty(GetCardIds(explore));
            Assert.DoesNotContain("href=\"/Recipe/Details/", home);
            Assert.Contains("/Recipe/Create", home);
            Assert.Contains("recipe-empty-container", explore);
        }

        [Theory]
        [InlineData("?q=PHO", "Beef pho", "Sweet pho")]
        [InlineData("?category=mÓN vIỆT", "Beef pho", "Rice bowl")]
        [InlineData("?q=PHO&category=mÓN vIỆT", "Beef pho", null)]
        public async Task Explore_ShouldFilterOnServerAndPreserveSearch(
            string query,
            string firstExpected,
            string? secondExpected)
        {
            await SeedRecipeAsync("Beef pho", category: "Món Việt");
            await SeedRecipeAsync("Sweet pho", category: "Món tráng miệng");
            await SeedRecipeAsync("Rice bowl", category: "Món Việt");
            using var client = _factory.CreateTestClient();

            var html = await GetHtmlAsync(client, "/Recipe" + query);
            var names = new[] { "Beef pho", "Sweet pho", "Rice bowl" };

            Assert.Contains(firstExpected, html);
            if (secondExpected != null)
            {
                Assert.Contains(secondExpected, html);
            }

            foreach (var excluded in names.Where(name => name != firstExpected && name != secondExpected))
            {
                Assert.DoesNotContain(excluded, html);
            }

            Assert.Equal(secondExpected == null ? 1 : 2, GetCardIds(html).Length);
            if (query.Contains("q=", StringComparison.Ordinal))
            {
                Assert.Matches("<input[^>]*name=\"q\"[^>]*value=\"PHO\"|<input[^>]*value=\"PHO\"[^>]*name=\"q\"", html);
            }

            if (query.Contains("category=", StringComparison.Ordinal))
            {
                Assert.Contains("mÓN vIỆT", html);
            }
        }

        [Fact]
        public async Task Explore_NoMatch_ShouldKeepSearchAndShowNoResults()
        {
            await SeedRecipeAsync("Beef pho");
            using var client = _factory.CreateTestClient();

            var html = await GetHtmlAsync(client, "/Recipe?q=no-matching-recipe&category=M%C3%B3n%20chay");

            Assert.Empty(GetCardIds(html));
            Assert.Contains("no-matching-recipe", html);
            Assert.Contains("recipe-empty-container", html);
            Assert.DoesNotContain("Beef pho", html);
        }

        [Fact]
        public async Task CreateThenEdit_ShouldKeepOneRecipeAndAuthorConsistentAcrossPages()
        {
            using var client = _factory.CreateTestClient(_factory.OwnerId);
            var form = ValidForm("Freshly published recipe");
            form["UserId"] = _factory.OtherUserId;
            form["CreatedAt"] = "2001-01-01";

            using var created = await PostFormAsync(client, "/Recipe/Create", form);

            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            var saved = Assert.Single(await ReadRecipesAsync());
            Assert.True(saved.Id > 0);
            Assert.Equal(_factory.OwnerId, saved.UserId);
            Assert.Equal($"/Recipe/Details/{saved.Id}", created.Headers.Location?.OriginalString);
            await AssertPublishedAcrossPagesAsync(client, saved.Id, form["Name"], form["Description"]);

            var edit = ValidForm("Edited published recipe");
            edit["Description"] = "Updated description across every page";
            edit["UserId"] = _factory.OtherUserId;
            edit["CreatedAt"] = "2002-01-01";
            using var updated = await PostFormAsync(client, $"/Recipe/Edit/{saved.Id}", edit);

            Assert.Equal(HttpStatusCode.Redirect, updated.StatusCode);
            Assert.Equal($"/Recipe/Details/{saved.Id}", updated.Headers.Location?.OriginalString);
            var persisted = Assert.Single(await ReadRecipesAsync());
            Assert.Equal(saved.Id, persisted.Id);
            Assert.Equal(saved.CreatedAt, persisted.CreatedAt);
            Assert.Equal(saved.UserId, persisted.UserId);
            Assert.Equal(edit["Image"], persisted.Image);
            await AssertPublishedAcrossPagesAsync(client, saved.Id, edit["Name"], edit["Description"]);
            using var refreshedClient = _factory.CreateTestClient();
            Assert.Contains(edit["Name"], await GetHtmlAsync(refreshedClient, $"/Recipe/Details/{saved.Id}"));
        }

        [Fact]
        public async Task AuthorProfile_ShouldShowOnlyAuthorsRecipesAndOwnerManagement()
        {
            var ownerRecipe = await SeedRecipeAsync("Owner recipe");
            await SeedRecipeAsync("Other user recipe", userId: _factory.OtherUserId);
            using var ownerClient = _factory.CreateTestClient(_factory.OwnerId);
            using var otherClient = _factory.CreateTestClient(_factory.OtherUserId);
            using var anonymousClient = _factory.CreateTestClient();

            var ownerProfile = await GetHtmlAsync(ownerClient, "/Profile");
            var publicProfile = await GetHtmlAsync(anonymousClient, "/Profile?userId=" + _factory.OwnerId);
            var otherView = await GetHtmlAsync(otherClient, "/Profile?userId=" + _factory.OwnerId);

            AssertRecipeLink(ownerProfile, ownerRecipe, "Owner recipe");
            Assert.Contains($"/Recipe/Edit/{ownerRecipe}", ownerProfile);
            Assert.Matches("id=\"profileRecipeCount\"[^>]*>\\s*1\\s*</strong>", ownerProfile);
            foreach (var profile in new[] { publicProfile, otherView })
            {
                AssertRecipeLink(profile, ownerRecipe, "Owner recipe");
                Assert.DoesNotContain("Other user recipe", profile);
                Assert.DoesNotContain($"/Recipe/Edit/{ownerRecipe}", profile);
                Assert.DoesNotContain($"/Recipe/Delete/{ownerRecipe}", profile);
            }
        }

        [Theory]
        [InlineData("Name", "")]
        [InlineData("Name", "   ")]
        [InlineData("Description", " ")]
        [InlineData("Category", "")]
        [InlineData("Ingredients", "")]
        [InlineData("Instructions", "")]
        [InlineData("Image", "javascript:alert(1)")]
        [InlineData("Image", "data:image/png;base64,AAAA")]
        [InlineData("Image", "//untrusted.example/image.png")]
        [InlineData("Image", "/img/../secret.png")]
        public async Task InvalidCreate_ShouldShowFieldErrorAndPreserveSubmittedValues(
            string field,
            string value)
        {
            using var client = _factory.CreateTestClient(_factory.OwnerId);
            var form = ValidForm("Retain submitted recipe");
            form[field] = value;

            using var response = await PostFormAsync(client, "/Recipe/Create", form);
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(await ReadRecipesAsync());
            Assert.Contains($"data-valmsg-for=\"{field}\"", html);
            Assert.Contains("field-validation-error", html);
            Assert.Contains(field == "Instructions" ? "Fresh ingredients" : "Preparation step one", html);
        }

        [Theory]
        [InlineData("Name", 201)]
        [InlineData("Category", 101)]
        [InlineData("Description", 2001)]
        [InlineData("Ingredients", 10001)]
        [InlineData("Instructions", 20001)]
        [InlineData("Image", 2001)]
        public async Task OverlengthCreate_ShouldNotPersist(string field, int length)
        {
            using var client = _factory.CreateTestClient(_factory.OwnerId);
            var form = ValidForm("Too long recipe");
            form[field] = new string('x', length);

            using var response = await PostFormAsync(client, "/Recipe/Create", form);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(await ReadRecipesAsync());
            Assert.Contains("field-validation-error", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task InvalidEdit_ShouldPreserveStoredRecipeAndKeepEditTarget()
        {
            var id = await SeedRecipeAsync("Original recipe");
            using var client = _factory.CreateTestClient(_factory.OwnerId);
            var form = ValidForm("Unsaved changed recipe");
            form["Image"] = "javascript:alert(1)";

            using var response = await PostFormAsync(client, $"/Recipe/Edit/{id}", form);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var stored = Assert.Single(await ReadRecipesAsync());
            Assert.Equal("Original recipe", stored.Name);
            Assert.Contains($"action=\"/Recipe/Edit/{id}\"", html);
            Assert.Contains("Unsaved changed recipe", html);
            Assert.Contains("field-validation-error", html);
        }

        [Theory]
        [InlineData("/Recipe/Create", "GET")]
        [InlineData("/Recipe/Create", "POST")]
        [InlineData("/Recipe/Edit/1", "GET")]
        [InlineData("/Recipe/Edit/1", "POST")]
        [InlineData("/Recipe/Delete/1", "POST")]
        public async Task AnonymousMutation_ShouldRequireLogin(string url, string method)
        {
            using var client = _factory.CreateTestClient();
            using var request = new HttpRequestMessage(new HttpMethod(method), url);

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString ?? "");
            Assert.Empty(await ReadRecipesAsync());
        }

        [Theory]
        [InlineData("Edit", "GET")]
        [InlineData("Edit", "POST")]
        [InlineData("Delete", "POST")]
        public async Task OtherUsersMutation_ShouldBeForbidden(string action, string method)
        {
            var id = await SeedRecipeAsync("Protected recipe");
            using var client = _factory.CreateTestClient(_factory.OtherUserId);
            using var response = method == "GET"
                ? await client.GetAsync($"/Recipe/{action}/{id}")
                : await PostFormAsync(client, $"/Recipe/{action}/{id}", ValidForm("Tampered recipe"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("Protected recipe", Assert.Single(await ReadRecipesAsync()).Name);
            var details = await GetHtmlAsync(client, $"/Recipe/Details/{id}");
            Assert.DoesNotContain($"/Recipe/Edit/{id}", details);
            Assert.DoesNotContain($"/Recipe/Delete/{id}", details);
        }

        [Theory]
        [InlineData("Create")]
        [InlineData("Edit")]
        [InlineData("Delete")]
        public async Task MutationWithoutAntiforgery_ShouldFail(string action)
        {
            var id = await SeedRecipeAsync("Protected recipe");
            using var client = _factory.CreateTestClient(_factory.OwnerId);
            var url = action == "Create" ? "/Recipe/Create" : $"/Recipe/{action}/{id}";

            using var response = await client.PostAsync(url, new FormUrlEncodedContent(ValidForm("Tampered")));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Protected recipe", Assert.Single(await ReadRecipesAsync()).Name);
        }

        [Theory]
        [InlineData("Details", "GET")]
        [InlineData("Edit", "GET")]
        [InlineData("Edit", "POST")]
        [InlineData("Delete", "POST")]
        public async Task MissingRecipe_ShouldReturnNotFound(string action, string method)
        {
            using var client = _factory.CreateTestClient(_factory.OwnerId);
            using var response = method == "GET"
                ? await client.GetAsync($"/Recipe/{action}/2147483647")
                : await PostFormAsync(client, $"/Recipe/{action}/2147483647", ValidForm("Missing"));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Delete_ShouldRemoveRecipeFromAllPublishedPages()
        {
            var id = await SeedRecipeAsync("Deleted published recipe");
            using var client = _factory.CreateTestClient(_factory.OwnerId);

            using var response = await PostFormAsync(client, $"/Recipe/Delete/{id}", new Dictionary<string, string>());

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Empty(await ReadRecipesAsync());
            foreach (var url in new[] { "/", "/Recipe", "/Profile" })
            {
                Assert.DoesNotContain("Deleted published recipe", await GetHtmlAsync(client, url));
            }

            using var details = await client.GetAsync($"/Recipe/Details/{id}");
            Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
        }

        [Fact]
        public async Task UnsafeStoredImage_ShouldUseAvailablePlaceholderAcrossPages()
        {
            var id = await SeedRecipeAsync("Unsafe legacy image", image: "javascript:alert(1)");
            using var client = _factory.CreateTestClient();

            foreach (var url in new[] { "/", "/Recipe", $"/Recipe/Details/{id}", "/Profile?userId=" + _factory.OwnerId })
            {
                var html = await GetHtmlAsync(client, url);
                Assert.Contains("src=\"/img/recipe-placeholder.svg\"", html);
                Assert.DoesNotContain("javascript:alert(1)", html);
            }

            using var placeholder = await client.GetAsync("/img/recipe-placeholder.svg");
            Assert.Equal(HttpStatusCode.OK, placeholder.StatusCode);
            Assert.Equal("image/svg+xml", placeholder.Content.Headers.ContentType?.MediaType);
        }

        private async Task AssertPublishedAcrossPagesAsync(HttpClient client, int id, string name, string description)
        {
            foreach (var url in new[] { "/", "/Recipe", "/Profile" })
            {
                var html = await GetHtmlAsync(client, url);
                AssertRecipeLink(html, id, name);
                Assert.Contains(description, html);
                Assert.Contains("https://images.example/recipe.png", html);
            }

            var details = await GetHtmlAsync(client, $"/Recipe/Details/{id}");
            Assert.Contains(name, details);
            Assert.Contains(description, details);
            Assert.Contains("Fresh ingredients", details);
            Assert.Contains("Preparation step one", details);
            Assert.Contains("Kitchen Owner", details);
            Assert.Contains("/Profile?userId=" + _factory.OwnerId, details);
        }

        private async Task<int> SeedRecipeAsync(
            string name,
            string category = "Món Việt",
            DateTime? createdAt = null,
            string? userId = null,
            string? image = null)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var recipe = new Recipe
            {
                Name = name,
                Description = "Database recipe description",
                Category = category,
                Ingredients = "Fresh ingredients",
                Instructions = "Preparation step one",
                UserId = userId ?? _factory.OwnerId,
                CreatedAt = createdAt ?? DateTime.UtcNow,
                Image = image
            };
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            return recipe.Id;
        }

        private async Task SeedLikeAsync(int recipeId, string userId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.RecipeLikes.Add(new RecipeLike
            {
                RecipeId = recipeId,
                UserId = userId
            });
            await db.SaveChangesAsync();
        }

        private async Task SeedRatingAsync(int recipeId, string userId, int stars)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.RecipeRatings.Add(new RecipeRating
            {
                RecipeId = recipeId,
                UserId = userId,
                Stars = stars
            });
            await db.SaveChangesAsync();
        }

        private async Task<List<Recipe>> ReadRecipesAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Recipes.AsNoTracking().ToListAsync();
        }

        private static Dictionary<string, string> ValidForm(string name)
        {
            return new Dictionary<string, string>
            {
                ["Name"] = name,
                ["Description"] = "Recipe description preserved across pages",
                ["Category"] = "Món Việt",
                ["Ingredients"] = "Fresh ingredients\nSecond ingredient",
                ["Instructions"] = "Preparation step one\nCooking step two",
                ["Image"] = "https://images.example/recipe.png"
            };
        }

        private static async Task<HttpResponseMessage> PostFormAsync(
            HttpClient client,
            string url,
            Dictionary<string, string> form)
        {
            using var tokenResponse = await client.GetAsync("/Recipe/Create");
            tokenResponse.EnsureSuccessStatusCode();
            var html = await tokenResponse.Content.ReadAsStringAsync();
            var createForm = Regex.Matches(html, @"<form\b[^>]*>[\s\S]*?</form>", RegexOptions.IgnoreCase)
                .Select(match => match.Value)
                .First(tag => tag.Contains("action=\"/Recipe/Create\"", StringComparison.Ordinal));
            var input = Regex.Matches(createForm, @"<input\b[^>]*>", RegexOptions.IgnoreCase)
                .Select(match => match.Value)
                .First(tag => tag.Contains("name=\"__RequestVerificationToken\"", StringComparison.Ordinal));
            var value = Regex.Match(input, "value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
            Assert.True(value.Success);
            form["__RequestVerificationToken"] = WebUtility.HtmlDecode(value.Groups[1].Value);
            return await client.PostAsync(url, new FormUrlEncodedContent(form));
        }

        private static async Task<string> GetHtmlAsync(HttpClient client, string url)
        {
            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        }

        private static void AssertRecipeLink(string html, int id, string name)
        {
            Assert.Contains(name, html);
            Assert.Contains($"href=\"/Recipe/Details/{id}\"", html);
        }

        private static string GetSectionContaining(string html, string gridId)
        {
            if (gridId is "homePopularGrid" or "homeTopRatedGrid")
            {
                return Regex.Match(html, $"<details id=\"{gridId}\">[\\s\\S]*?</details>").Value;
            }

            return Regex.Matches(html, @"<section\b[^>]*>[\s\S]*?</section>", RegexOptions.IgnoreCase)
                .Select(match => match.Value)
                .Single(section => section.Contains($"id=\"{gridId}\"", StringComparison.Ordinal));
        }

        private static int[] GetCardIds(string html)
        {
            return Regex.Matches(html, "<article\\b[^>]*\\bdata-recipe-id=\"(\\d+)\"", RegexOptions.IgnoreCase)
                .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
                .ToArray();
        }

        private static int[] GetDiscoveryIds(string html)
        {
            return Regex.Matches(html, "data-recipe-id=\"(\\d+)\"")
                .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
        }
    }
}
