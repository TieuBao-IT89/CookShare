using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Integration
{
    public sealed class CreatorProfileIntegrationTests : IAsyncLifetime
    {
        private readonly IsolatedRecipeWebApplicationFactory _factory = new();

        public async Task InitializeAsync()
        {
            await _factory.InitializeAsync();
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var user in await db.Users.ToListAsync())
            {
                user.Email = user.Id + "@example.com";
                user.NormalizedEmail = user.Email.ToUpperInvariant();
            }

            await db.SaveChangesAsync();
        }

        public async Task DisposeAsync() => await _factory.DisposeAsync();

        [Fact]
        public async Task Edit_ShouldPersistOwnBioAndCoverAndIgnoreAccountSpoofing()
        {
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var response = await PostAsync(owner, "/Profile/Edit", new()
            {
                ["DisplayName"] = "Bếp nhà mình",
                ["AvatarUrl"] = "/img/recipes/profile-avatar.webp",
                ["CoverImageUrl"] = "/img/recipes/pho.webp",
                ["Bio"] = "Yêu bếp Việt\r\nMỗi món ăn là một câu chuyện <3",
                ["UserId"] = _factory.OtherUserId,
                ["UserName"] = "forged",
                ["Email"] = "forged@example.com",
                ["PasswordHash"] = "forged"
            });
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(item => item.Id == _factory.OwnerId);
            Assert.Equal("Bếp nhà mình", user.DisplayName);
            Assert.Equal("Yêu bếp Việt\nMỗi món ăn là một câu chuyện <3", user.Bio);
            Assert.Equal("/img/recipes/pho.webp", user.CoverImageUrl);
            Assert.Equal("kitchen-owner", user.UserName);
            Assert.Equal(_factory.OwnerId + "@example.com", user.Email);
            Assert.Null(user.PasswordHash);
            Assert.Null((await db.Users.SingleAsync(item => item.Id == _factory.OtherUserId)).Bio);
            using var guest = _factory.CreateTestClient();
            string html = await guest.GetStringAsync("/Profile?userId=" + _factory.OwnerId);
            Assert.Contains("creator-cover-image", html);
            Assert.Contains("&lt;3", html);
            Assert.DoesNotContain("/Profile/Highlights", html);
            Assert.DoesNotContain("id=\"favorites\"", html);
        }

        [Theory]
        [InlineData("Bio", "<script>alert(1)</script>")]
        [InlineData("CoverImageUrl", "javascript:alert(1)")]
        [InlineData("CoverImageUrl", "http://example.com/cover.webp")]
        [InlineData("AvatarUrl", "//example.com/avatar.webp")]
        public async Task InvalidProfile_ShouldPreserveFormAndStoredData(string field, string value)
        {
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            var values = new Dictionary<string, string> { ["DisplayName"] = "New name", ["Bio"] = "Nội dung đang nhập", ["CoverImageUrl"] = "/img/recipes/pho.webp" };
            values[field] = value;
            using var response = await PostAsync(owner, "/Profile/Edit", values);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string html = await response.Content.ReadAsStringAsync();
            Assert.Contains("New name", html);
            Assert.Contains("validation-summary-errors", html);
            Assert.DoesNotContain("<script>alert(1)</script>", html);
            using var scope = _factory.Services.CreateScope();
            var user = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(item => item.Id == _factory.OwnerId);
            Assert.Equal("Kitchen Owner", user.DisplayName);
            Assert.Null(user.Bio);
            Assert.Null(user.CoverImageUrl);
        }

        [Fact]
        public async Task TooLongBio_ShouldBeRejectedAndInputKept()
        {
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            string bio = new('a', 301);
            using var response = await PostAsync(owner, "/Profile/Edit", new() { ["DisplayName"] = "Kitchen Owner", ["Bio"] = bio });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(bio, await response.Content.ReadAsStringAsync());
            using var scope = _factory.Services.CreateScope();
            Assert.Null((await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(item => item.Id == _factory.OwnerId)).Bio);
        }

        [Fact]
        public async Task Highlights_ShouldPinReorderAndUnpinOnlyOwnRecipesWithoutChangingRecipes()
        {
            int[] own = await RecipesAsync(_factory.OwnerId, 3);
            int foreign = (await RecipesAsync(_factory.OtherUserId, 1))[0];
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var other = _factory.CreateTestClient(_factory.OtherUserId);
            using var saved = await PinAsync(owner, new[] { own[2], own[0], own[1] }, _factory.OtherUserId);
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            Assert.Equal(new[] { own[2], own[0], own[1] }, await PinIdsAsync(_factory.OwnerId));
            using var reordered = await PinAsync(owner, new[] { own[1], own[2], 0 });
            Assert.Equal(HttpStatusCode.Redirect, reordered.StatusCode);
            Assert.Equal(new[] { own[1], own[2] }, await PinIdsAsync(_factory.OwnerId));
            using var otherSave = await PinAsync(other, new[] { foreign });
            Assert.Equal(HttpStatusCode.Redirect, otherSave.StatusCode);
            Assert.Equal(new[] { foreign }, await PinIdsAsync(_factory.OtherUserId));
            string manager = await owner.GetStringAsync("/Profile/Highlights?userId=" + _factory.OtherUserId);
            Assert.DoesNotContain($"value=\"{foreign}\"", manager);
            using var cleared = await PinAsync(owner, new[] { 0, 0, 0 });
            Assert.Equal(HttpStatusCode.Redirect, cleared.StatusCode);
            Assert.Empty(await PinIdsAsync(_factory.OwnerId));
            Assert.Equal(new[] { foreign }, await PinIdsAsync(_factory.OtherUserId));
            using var scope = _factory.Services.CreateScope();
            var recipes = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Recipes.OrderBy(recipe => recipe.Id).ToListAsync();
            Assert.Equal(4, recipes.Count);
            Assert.All(recipes.Where(recipe => recipe.UserId == _factory.OwnerId), recipe => Assert.StartsWith("Owned recipe", recipe.Name));
        }

        [Theory]
        [InlineData("duplicate")]
        [InlineData("four")]
        [InlineData("foreign")]
        [InlineData("missing")]
        [InlineData("negative")]
        public async Task InvalidHighlights_ShouldLeavePreviousSelectionIntact(string kind)
        {
            int[] own = await RecipesAsync(_factory.OwnerId, 4);
            int foreign = (await RecipesAsync(_factory.OtherUserId, 1))[0];
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var initial = await PinAsync(owner, new[] { own[1] });
            Assert.Equal(HttpStatusCode.Redirect, initial.StatusCode);
            int[] invalid = kind switch { "duplicate" => new[] { own[0], own[0] }, "four" => own, "foreign" => new[] { foreign }, "negative" => new[] { -1 }, _ => new[] { int.MaxValue } };
            using var rejected = await PinAsync(owner, invalid);
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            Assert.Contains("validation-summary-errors", await rejected.Content.ReadAsStringAsync());
            Assert.Equal(new[] { own[1] }, await PinIdsAsync(_factory.OwnerId));
        }

        [Fact]
        public async Task Highlights_ShouldRenderOrderedCardsWithRealRatingsAndNoFakeCards()
        {
            int[] own = await RecipesAsync(_factory.OwnerId, 2);
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var guest = _factory.CreateTestClient();
            string empty = await guest.GetStringAsync("/Profile?userId=" + _factory.OwnerId);
            Assert.DoesNotContain("data-highlight-card", empty);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.RecipeRatings.Add(new RecipeRating { RecipeId = own[0], UserId = _factory.OtherUserId, Stars = 5 });
                await db.SaveChangesAsync();
            }

            using var saved = await PinAsync(owner, new[] { own[1], own[0] });
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            string html = await guest.GetStringAsync("/Profile?userId=" + _factory.OwnerId);
            var matches = Regex.Matches(html, "data-highlight-card[^>]*data-recipe-id=\"(\\d+)\"");
            Assert.Equal(new[] { own[1], own[0] }, matches.Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)));
            Assert.Contains("data-average-rating=\"5\"", html);
            Assert.DoesNotContain("id=\"favorites\"", html);
        }

        [Theory]
        [InlineData("/Profile/Edit")]
        [InlineData("/Profile/Highlights")]
        public async Task Editing_ShouldRequireAuthenticationAndCsrf(string path)
        {
            using var guest = _factory.CreateTestClient();
            using var get = await guest.GetAsync(path);
            using var post = await guest.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.Redirect, get.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var csrf = await owner.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        }

        private async Task<int[]> RecipesAsync(string author, int count)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var recipes = Enumerable.Range(0, count).Select(index => new Recipe { Name = "Owned recipe " + index, UserId = author, Description = "Recipe story", Category = "Món Việt", Ingredients = "Rice", Instructions = "Cook" }).ToArray();
            db.Recipes.AddRange(recipes); await db.SaveChangesAsync(); return recipes.Select(recipe => recipe.Id).ToArray();
        }

        private async Task<int[]> PinIdsAsync(string author)
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ProfileFeaturedRecipes.AsNoTracking()
                .Where(pin => pin.UserId == author).OrderBy(pin => pin.DisplayOrder).Select(pin => pin.RecipeId).ToArrayAsync();
        }

        private static Task<HttpResponseMessage> PinAsync(HttpClient client, int[] ids, string? spoofUser = null)
        {
            var values = new Dictionary<string, string>();
            for (int index = 0; index < ids.Length; index++) values[$"RecipeIds[{index}]"] = ids[index].ToString(CultureInfo.InvariantCulture);
            if (spoofUser != null) values["UserId"] = spoofUser;
            return PostAsync(client, "/Profile/Highlights", values);
        }

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, Dictionary<string, string> values)
        {
            string html = await client.GetStringAsync("/Profile/Edit");
            string token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.False(string.IsNullOrEmpty(token));
            values["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
            return await client.PostAsync(path, new FormUrlEncodedContent(values));
        }
    }
}
