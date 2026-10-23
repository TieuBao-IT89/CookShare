using System.Net;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Data.Seeding;
using CookShare.Models;
using CookShare.Tests.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Integration
{
    public sealed class RecipeSeedingIntegrationTests : IAsyncLifetime
    {
        private readonly IsolatedRecipeWebApplicationFactory _factory = new();

        public async Task InitializeAsync()
        {
            await _factory.InitializeAsync();
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var user in await db.Users.ToListAsync())
            {
                user.NormalizedUserName = new UpperInvariantLookupNormalizer().NormalizeName(user.UserName);
            }

            await db.SaveChangesAsync();
        }

        public async Task DisposeAsync() => await _factory.DisposeAsync();

        [Fact]
        public async Task StartupAndRequests_ShouldNeverSeedAutomatically()
        {
            using var client = _factory.CreateTestClient();
            using var home = await client.GetAsync("/");
            using var index = await client.GetAsync("/Recipe");
            home.EnsureSuccessStatusCode();
            index.EnsureSuccessStatusCode();
            using var scope = _factory.Services.CreateScope();
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Recipes.CountAsync());
            using var seedEndpoint = await client.GetAsync("/seed-recipes");
            Assert.Equal(HttpStatusCode.NotFound, seedEndpoint.StatusCode);
        }

        [Fact]
        public async Task LocalSeed_ShouldAppearInHomeExploreDetailsSearchAndCorrectAuthorProfiles()
        {
            await SeedAsync();
            using var client = _factory.CreateTestClient();
            var recipes = await RecipesAsync();
            var pho = recipes.Single(recipe => recipe.ExternalId == "pho-bo");
            var newest = recipes.MaxBy(recipe => recipe.CreatedAt)!;

            Assert.Contains(newest.Name, await HtmlAsync(client, "/"));
            string index = await HtmlAsync(client, "/Recipe");
            Assert.Contains("data-recipe-id=", index);
            string search = await HtmlAsync(client, "/Recipe?q=" + Uri.EscapeDataString("Phở bò"));
            Assert.Contains(pho.Name, search);
            Assert.DoesNotContain("Cơm gà gừng", search);
            string category = await HtmlAsync(client, "/Recipe?category=" + Uri.EscapeDataString("Món chay"));
            Assert.Contains("Đậu hũ sốt nấm hương", category);
            Assert.DoesNotContain(pho.Name, category);
            string details = await HtmlAsync(client, $"/Recipe/Details/{pho.Id}");
            Assert.Contains("/img/recipes/pho.webp", details);
            Assert.Contains("500 g thịt bò phần nạm", details);
            Assert.Contains("Chần xương bò", details);

            foreach (string userId in new[] { _factory.OwnerId, _factory.OtherUserId })
            {
                string profile = await HtmlAsync(client, "/Profile?userId=" + userId);
                Assert.Matches("id=\"profileRecipeCount\"[^>]*>\\s*15\\s*</strong>", profile);
                var own = recipes.Where(recipe => recipe.UserId == userId).Select(recipe => recipe.Id).ToHashSet();
                int[] displayed = Regex.Matches(profile, "data-recipe-id=\"(\\d+)\"")
                    .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                Assert.NotEmpty(displayed);
                Assert.All(displayed, id => Assert.Contains(id, own));
            }
        }

        [Fact]
        public async Task SeededRecipe_ShouldSupportLikesCommentsAndFavorites()
        {
            await SeedAsync();
            var recipe = (await RecipesAsync()).First(item => item.UserId == _factory.OwnerId);
            using var client = _factory.CreateTestClient(_factory.OtherUserId);
            using var liked = await PostAsync(client, $"/Recipe/{recipe.Id}/Likes");
            using var commented = await PostAsync(client, $"/Recipe/{recipe.Id}/Comments", "Content", "Tried this seeded recipe");
            using var saved = await PostAsync(client, $"/Recipe/{recipe.Id}/Favorites");
            liked.EnsureSuccessStatusCode();
            commented.EnsureSuccessStatusCode();
            saved.EnsureSuccessStatusCode();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(_factory.OtherUserId, (await db.RecipeLikes.SingleAsync()).UserId);
            Assert.Equal(recipe.Id, (await db.RecipeComments.SingleAsync()).RecipeId);
            Assert.Equal(recipe.Id, (await db.Favorites.SingleAsync()).RecipeId);
            Assert.Contains("Tried this seeded recipe", await HtmlAsync(client, $"/Recipe/Details/{recipe.Id}"));
            Assert.Contains(recipe.Name, await HtmlAsync(client, "/Recipe/Saved"));
        }

        [Fact]
        public async Task ImportedRecipe_ShouldShowAttributionAndRejectUnsafeReferenceLinks()
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var source = new StubRecipeSeedSource(RecipeSeedTestSupport.Record() with
                {
                    Key = "52772",
                    SourceUrl = "https://example.com/original"
                });
                var seeder = RecipeSeedTestSupport.Seeder(db, source);
                await seeder.RunAsync(new RecipeSeedOptions { Count = 1, UserNames = new[] { "kitchen-owner" } });
                var recipe = await db.Recipes.SingleAsync();
                recipe.Source = "themealdb";
                await db.SaveChangesAsync();
            }

            var imported = Assert.Single(await RecipesAsync());
            using var client = _factory.CreateTestClient();
            string html = await HtmlAsync(client, $"/Recipe/Details/{imported.Id}");
            Assert.Contains("https://www.themealdb.com/meal.php?c=52772", html);
            Assert.Contains("https://example.com/original", html);
            Assert.Contains("Nguồn công thức", html);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                (await db.Recipes.SingleAsync()).SourceUrl = "javascript:alert(12345)";
                await db.SaveChangesAsync();
            }

            html = await HtmlAsync(client, $"/Recipe/Details/{imported.Id}");
            Assert.DoesNotContain("javascript:alert(12345)", html);
        }

        private async Task SeedAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<RecipeSeeder>().RunAsync(new RecipeSeedOptions
            {
                UserNames = new[] { "kitchen-owner", "other-cook" }
            });
            Assert.Equal(30, result.Added);
            Assert.Empty(result.Errors);
        }

        private async Task<List<Recipe>> RecipesAsync()
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Recipes.AsNoTracking().ToListAsync();
        }

        private static async Task<string> HtmlAsync(HttpClient client, string url)
        {
            return WebUtility.HtmlDecode(await client.GetStringAsync(url));
        }

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, string? name = null, string? value = null)
        {
            string html = await client.GetStringAsync("/Recipe/Create");
            string input = Regex.Matches(html, @"<input\b[^>]*>", RegexOptions.IgnoreCase)
                .Select(match => match.Value).First(tag => tag.Contains("name=\"__RequestVerificationToken\"", StringComparison.Ordinal));
            string token = WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value);
            var form = new Dictionary<string, string> { ["__RequestVerificationToken"] = token };
            if (name != null)
            {
                form[name] = value!;
            }

            return await client.PostAsync(url, new FormUrlEncodedContent(form));
        }
    }
}
