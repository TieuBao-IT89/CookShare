using System.Net;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Integration
{
    public sealed class AccountProfileIntegrationTests : IClassFixture<AccountSettingsWebApplicationFactory>
    {
        private readonly AccountSettingsWebApplicationFactory _factory;

        public AccountProfileIntegrationTests(AccountSettingsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ProfileEdit_ShouldUpdateSettingsAndRecipeAuthorWithoutChangingAnotherAccount()
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var owner = await CreateUserAsync(users, "Tên tác giả cũ");
            var other = await CreateUserAsync(users, "Hồ sơ cần giữ nguyên");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var recipe = new Recipe
            {
                Name = "Món của tác giả cập nhật hồ sơ",
                Category = "Món Việt",
                Ingredients = "Rau",
                Instructions = "Nấu chín",
                UserId = owner.Id
            };
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();

            using var client = _factory.CreateTestClient(owner.Id);
            var editPage = await client.GetStringAsync("/Profile/Edit");
            var token = WebUtility.HtmlDecode(Regex.Match(editPage,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            Assert.NotEmpty(token);

            const string displayName = "Bếp nhà sau khi cập nhật";
            const string avatar = "/img/recipes/profile-avatar.webp";
            using var edited = await client.PostAsync("/Profile/Edit", new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["DisplayName"] = displayName,
                    ["AvatarUrl"] = avatar,
                    ["UserId"] = other.Id,
                    ["Email"] = "forged@example.com"
                }));
            Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);

            foreach (var path in new[] { "/Account/Settings", "/Profile", "/Recipe/Details/" + recipe.Id, "/Recipe" })
            {
                using var page = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, page.StatusCode);
                var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
                Assert.Contains(displayName, html, StringComparison.Ordinal);
                Assert.DoesNotContain("Tên tác giả cũ", html, StringComparison.Ordinal);
            }

            db.ChangeTracker.Clear();
            var saved = await db.Users.SingleAsync(user => user.Id == owner.Id);
            Assert.Equal(displayName, saved.DisplayName);
            Assert.Equal(avatar, saved.AvatarUrl);
            Assert.Equal(owner.Email, saved.Email);
            var untouched = await db.Users.SingleAsync(user => user.Id == other.Id);
            Assert.Equal("Hồ sơ cần giữ nguyên", untouched.DisplayName);
            Assert.Equal(other.Email, untouched.Email);
            Assert.Null(untouched.AvatarUrl);
        }

        private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> users, string name)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var user = new ApplicationUser
            {
                UserName = "profile_" + suffix,
                Email = suffix + "@example.com",
                DisplayName = name
            };
            var result = await users.CreateAsync(user, "TestPassword123!");
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
            return user;
        }
    }
}
