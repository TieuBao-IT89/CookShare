using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Integration
{
    public sealed class RecipeCollectionsIntegrationTests : IAsyncLifetime
    {
        private readonly IsolatedRecipeWebApplicationFactory _factory = new();

        public Task InitializeAsync() => _factory.InitializeAsync();

        public async Task DisposeAsync() => await _factory.DisposeAsync();

        [Fact]
        public async Task PrivateCollection_ShouldRejectDirectAccessAndOnlyExposePublicCollectionAfterExplicitEdit()
        {
            int recipeId = await SeedRecipeAsync("Món của căn bếp");
            int collectionId = await SeedCollectionAsync("Bí mật bữa tối", recipeId);
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var other = _factory.CreateTestClient(_factory.OtherUserId);
            using var guest = _factory.CreateTestClient();
            foreach (var viewer in new[] { other, guest })
            {
                using var denied = await viewer.GetAsync($"/Collections/{collectionId}");
                Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
                Assert.DoesNotContain("Bí mật bữa tối", await denied.Content.ReadAsStringAsync());
            }
            string privatePage = await owner.GetStringAsync($"/Collections/{collectionId}");
            Assert.Contains($"data-recipe-id=\"{recipeId}\"", privatePage);
            Assert.Contains("Chỉ mình bạn", WebUtility.HtmlDecode(privatePage));

            using var published = await PostAsync(owner, $"/Collections/{collectionId}/Edit", new()
            {
                ["Name"] = "Bữa tối công khai",
                ["IsPublic"] = "true",
                ["OwnerId"] = _factory.OtherUserId
            });
            Assert.Equal(HttpStatusCode.Redirect, published.StatusCode);
            foreach (var viewer in new[] { other, guest })
            {
                using var publicPage = await viewer.GetAsync($"/Collections/{collectionId}");
                publicPage.EnsureSuccessStatusCode();
                Assert.True(publicPage.Headers.CacheControl?.NoStore);
                Assert.Contains("Bữa tối công khai", WebUtility.HtmlDecode(await publicPage.Content.ReadAsStringAsync()));
            }
            using var hiddenAgain = await PostAsync(owner, $"/Collections/{collectionId}/Edit", new() { ["Name"] = "Bữa tối công khai", ["IsPublic"] = "false" });
            Assert.Equal(HttpStatusCode.Redirect, hiddenAgain.StatusCode);
            using var deniedAgain = await guest.GetAsync($"/Collections/{collectionId}");
            Assert.Equal(HttpStatusCode.NotFound, deniedAgain.StatusCode);
        }

        [Fact]
        public async Task CreateAndChoose_ShouldPersistPrivateCollectionAndRecipeAtomicallyWithoutSavingFavorite()
        {
            int recipeId = await SeedRecipeAsync("Bún cuối tuần");
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var created = await PostAsync(owner, "/Collections/Create", new()
            {
                ["Name"] = "  Món muốn thử  ",
                ["RecipeId"] = recipeId.ToString(CultureInfo.InvariantCulture),
                ["OwnerId"] = _factory.OtherUserId,
                ["IsPublic"] = "true",
                ["ReturnUrl"] = "https://outside.example/"
            });
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            Assert.StartsWith($"/Recipe/{recipeId}/Collections", created.Headers.Location?.OriginalString);
            Assert.DoesNotContain("outside.example", created.Headers.Location?.OriginalString ?? "");
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = Assert.Single(await db.Set<RecipeCollection>().AsNoTracking().ToListAsync());
            Assert.Equal(_factory.OwnerId, stored.OwnerId);
            Assert.Equal("Món muốn thử", stored.Name);
            Assert.False(stored.IsPublic);
            Assert.Equal(recipeId, Assert.Single(await db.Set<CollectionRecipe>().AsNoTracking().ToListAsync()).RecipeId);
            Assert.Empty(await db.Favorites.ToListAsync());
            using var restarted = _factory.CreateTestClient(_factory.OwnerId);
            string chooser = await restarted.GetStringAsync($"/Recipe/{recipeId}/Collections");
            Assert.Contains("Món muốn thử", WebUtility.HtmlDecode(chooser));
            Assert.Contains("aria-pressed=\"true\"", chooser);
        }

        [Fact]
        public async Task MembershipAndFavorite_ShouldStayIndependentAcrossRepeatsAndSeparateCollections()
        {
            int recipeId = await SeedRecipeAsync("Phở thơm");
            int first = await SeedCollectionAsync("Bữa sáng");
            int second = await SeedCollectionAsync("Cuối tuần");
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                foreach (int collectionId in new[] { first, second })
                {
                    using var added = await PostAsync(owner, $"/Collections/{collectionId}/Recipes/{recipeId}/Add");
                    Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
                }
            }
            using var saved = await PostAsync(owner, $"/Recipe/{recipeId}/Favorites");
            saved.EnsureSuccessStatusCode();
            using var unsaved = await PostAsync(owner, $"/Recipe/{recipeId}/Favorites/Unsave");
            unsaved.EnsureSuccessStatusCode();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Equal(2, await db.Set<CollectionRecipe>().CountAsync());
                Assert.Empty(await db.Favorites.ToListAsync());
            }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var removed = await PostAsync(owner, $"/Collections/{first}/Recipes/{recipeId}/Remove");
                Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
            }
            using var secondScope = _factory.Services.CreateScope();
            var verify = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(second, Assert.Single(await verify.Set<CollectionRecipe>().AsNoTracking().ToListAsync()).CollectionId);
            Assert.Single(await verify.Recipes.ToListAsync());
        }

        [Theory]
        [InlineData("Edit")]
        [InlineData("Delete")]
        [InlineData("Add")]
        [InlineData("Remove")]
        public async Task CrossOwnerMutation_ShouldReturn404EvenForPublicCollectionAndForgedOwner(string action)
        {
            int recipeId = await SeedRecipeAsync("Món thật");
            int collectionId = await SeedCollectionAsync("Bộ của A", recipeId, true);
            using var other = _factory.CreateTestClient(_factory.OtherUserId);
            string path = action is "Add" or "Remove" ? $"/Collections/{collectionId}/Recipes/{recipeId}/{action}" : $"/Collections/{collectionId}/{action}";
            using var denied = await PostAsync(other, path, new() { ["Name"] = "Chiếm quyền", ["OwnerId"] = _factory.OwnerId });
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal("Bộ của A", Assert.Single(await db.Set<RecipeCollection>().ToListAsync()).Name);
            Assert.Single(await db.Set<CollectionRecipe>().ToListAsync());
        }

        [Fact]
        public async Task MembershipRouteIds_ShouldIgnoreForgedFormIds()
        {
            int firstRecipe = await SeedRecipeAsync("First");
            int secondRecipe = await SeedRecipeAsync("Second");
            int first = await SeedCollectionAsync("First collection");
            int second = await SeedCollectionAsync("Second collection");
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var added = await PostAsync(owner, $"/Collections/{first}/Recipes/{firstRecipe}/Add", new()
            {
                ["id"] = second.ToString(CultureInfo.InvariantCulture),
                ["recipeId"] = secondRecipe.ToString(CultureInfo.InvariantCulture)
            });
            Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
            using var scope = _factory.Services.CreateScope();
            var membership = Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<CollectionRecipe>().ToListAsync());
            Assert.Equal(first, membership.CollectionId);
            Assert.Equal(firstRecipe, membership.RecipeId);
        }

        [Fact]
        public async Task Chooser_ShouldNeverListOtherAccountsPrivateOrPublicCollections()
        {
            int recipeId = await SeedRecipeAsync("Món cần lưu");
            await SeedCollectionAsync("Tuyệt mật của A");
            await SeedCollectionAsync("Công khai của A", isPublic: true);
            using var other = _factory.CreateTestClient(_factory.OtherUserId);
            other.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
            using var response = await other.GetAsync($"/Recipe/{recipeId}/Collections");
            response.EnsureSuccessStatusCode();
            string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.DoesNotContain("Tuyệt mật của A", html);
            Assert.DoesNotContain("Công khai của A", html);
            Assert.DoesNotContain("<html", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Tạo bộ sưu tập mới", html);
        }

        [Fact]
        public async Task HiddenRecipe_ShouldDisappearFromCollectionCountCoverAndReadAndRejectNewMembership()
        {
            int visibleRecipe = await SeedRecipeAsync("Món hiển thị");
            int hiddenRecipe = await SeedRecipeAsync("Món bị ẩn", true);
            int collectionId = await SeedCollectionAsync("Bộ công khai", visibleRecipe, true);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Set<CollectionRecipe>().Add(new CollectionRecipe { CollectionId = collectionId, RecipeId = hiddenRecipe });
                await db.SaveChangesAsync();
            }
            using var guest = _factory.CreateTestClient();
            string html = await guest.GetStringAsync($"/Collections/{collectionId}");
            Assert.Contains($"data-recipe-id=\"{visibleRecipe}\"", html);
            Assert.DoesNotContain($"data-recipe-id=\"{hiddenRecipe}\"", html);
            Assert.DoesNotContain("hidden-media-test.jpg", html);
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var hiddenChooser = await owner.GetAsync($"/Recipe/{hiddenRecipe}/Collections");
            Assert.Equal(HttpStatusCode.NotFound, hiddenChooser.StatusCode);
            using var hiddenAdd = await PostAsync(owner, $"/Collections/{collectionId}/Recipes/{hiddenRecipe}/Add");
            Assert.Equal(HttpStatusCode.NotFound, hiddenAdd.StatusCode);
            string index = await owner.GetStringAsync("/Collections");
            Assert.Contains("1 công thức", WebUtility.HtmlDecode(index));
            Assert.DoesNotContain("hidden-media-test.jpg", index);
        }

        [Fact]
        public async Task CollectionForms_ShouldRequireAuthenticationAndCsrfAndEncodeNames()
        {
            int collectionId = await SeedCollectionAsync("<script>alert('collection')</script>");
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var guest = _factory.CreateTestClient();
            using var privatePage = await guest.GetAsync("/Collections");
            Assert.Equal(HttpStatusCode.Redirect, privatePage.StatusCode);
            foreach (string path in new[] { "/Collections/Create", $"/Collections/{collectionId}/Edit", $"/Collections/{collectionId}/Delete", $"/Collections/{collectionId}/Recipes/1/Add", $"/Collections/{collectionId}/Recipes/1/Remove" })
            {
                using var noToken = await owner.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()));
                Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
            }
            string html = await owner.GetStringAsync("/Collections");
            Assert.DoesNotContain("<script>alert('collection')</script>", html);
            Assert.Contains("&lt;script&gt;", html);
        }

        [Fact]
        public async Task InvalidInlineCreate_ShouldKeepNativeFormOpenPreserveInputAndCreateNothing()
        {
            int recipeId = await SeedRecipeAsync("Món giữ lại");
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            string name = new('a', 81);
            using var response = await PostAsync(owner, "/Collections/Create", new()
            {
                ["Name"] = name,
                ["RecipeId"] = recipeId.ToString(CultureInfo.InvariantCulture),
                ["Description"] = "Mô tả giữ lại"
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            string html = await response.Content.ReadAsStringAsync();
            Assert.Matches("<details[^>]*class=\"collection-create-inline\"[^>]*open", html);
            Assert.Contains(name, html);
            Assert.Contains("Mô tả giữ lại", WebUtility.HtmlDecode(html));
            Assert.Contains("validation-summary-errors", html);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await db.Set<RecipeCollection>().ToListAsync());
            Assert.Empty(await db.Set<CollectionRecipe>().ToListAsync());
        }

        [Fact]
        public async Task FavoriteNativeReturn_ShouldUseOnlyLocalUrlsAndKeepAjaxJsonContract()
        {
            int recipeId = await SeedRecipeAsync("Lưu riêng");
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            string chooser = $"/Recipe/{recipeId}/Collections";
            using var local = await PostAsync(owner, $"/Recipe/{recipeId}/Favorites", new() { ["returnUrl"] = chooser });
            Assert.Equal(HttpStatusCode.Redirect, local.StatusCode);
            Assert.Equal(chooser, local.Headers.Location?.OriginalString);
            using var external = await PostAsync(owner, $"/Recipe/{recipeId}/Favorites", new() { ["returnUrl"] = "//outside.example/path" });
            external.EnsureSuccessStatusCode();
            Assert.Equal("application/json", external.Content.Headers.ContentType?.MediaType);
            owner.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
            using var ajax = await PostAsync(owner, $"/Recipe/{recipeId}/Favorites/Unsave", new() { ["returnUrl"] = chooser });
            ajax.EnsureSuccessStatusCode();
            var json = JsonSerializer.Deserialize<JsonElement>(await ajax.Content.ReadAsStringAsync());
            Assert.False(json.GetProperty("isSaved").GetBoolean());
        }

        private async Task<int> SeedRecipeAsync(string name, bool hidden = false)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var recipe = new Recipe { UserId = _factory.OwnerId, Name = name, Category = "Món Việt", Description = "Món nhà", Ingredients = "Rau", Instructions = "Nấu", IsHidden = hidden, Image = hidden ? "/uploads/recipes/hidden-media-test.jpg" : null };
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            return recipe.Id;
        }

        private async Task<int> SeedCollectionAsync(string name, int? recipeId = null, bool isPublic = false)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var collection = new RecipeCollection { OwnerId = _factory.OwnerId, Name = name, IsPublic = isPublic };
            if (recipeId.HasValue) collection.Recipes.Add(new CollectionRecipe { RecipeId = recipeId.Value });
            db.Set<RecipeCollection>().Add(collection);
            await db.SaveChangesAsync();
            return collection.Id;
        }

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, Dictionary<string, string>? fields = null)
        {
            using var page = await client.GetAsync("/Collections/Create");
            page.EnsureSuccessStatusCode();
            string html = await page.Content.ReadAsStringAsync();
            string input = Regex.Matches(html, @"<input\b[^>]*>", RegexOptions.IgnoreCase).Select(match => match.Value)
                .First(tag => tag.Contains("name=\"__RequestVerificationToken\"", StringComparison.Ordinal));
            var token = Regex.Match(input, "value=\"([^\"]+)\"");
            var data = fields == null ? new Dictionary<string, string>() : new Dictionary<string, string>(fields);
            data["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
            return await client.PostAsync(path, new FormUrlEncodedContent(data));
        }
    }
}
