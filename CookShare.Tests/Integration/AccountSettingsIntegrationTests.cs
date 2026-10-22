using System.Net;
using System.Text.RegularExpressions;
using CookShare.Controllers;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CookShare.Tests.Integration
{
    public class AccountSettingsIntegrationTests
        : IClassFixture<CookShareWebApplicationFactory>
    {
        private const string Password = "TestPassword123!";

        private readonly CookShareWebApplicationFactory _factory;

        public AccountSettingsIntegrationTests(
            CookShareWebApplicationFactory factory)
        {
            _factory = factory;
            _factory.Emails.Messages.Clear();
        }

        [Fact]
        public void Password_reset_tokens_expire_after_one_hour()
        {
            using var scope = _factory.Services.CreateScope();
            var options = scope.ServiceProvider
                .GetRequiredService<
                    IOptions<DataProtectionTokenProviderOptions>>();

            Assert.Equal(
                TimeSpan.FromHours(1),
                options.Value.TokenLifespan);
        }

        [Fact]
        public async Task Settings_requires_authentication()
        {
            using var client = _factory.CreateTestClient();

            var response = await client.GetAsync("/Account/Settings");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains(
                "/Account/Login",
                response.Headers.Location?.OriginalString,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateDisplayName_changes_only_the_signed_in_user()
        {
            var owner = await CreateUserAsync("Tên cũ");
            var other = await CreateUserAsync("Người khác");
            using var client = _factory.CreateTestClient(owner.Id);
            var token = await GetTokenAsync(client, "/Account/Settings");

            var response = await PostAsync(
                client,
                "/Account/UpdateDisplayName",
                token,
                new Dictionary<string, string>
                {
                    ["DisplayNameForm.DisplayName"] = "Bếp nhà"
                });

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var savedOwner = await FindUserAsync(owner.Id);
            var savedOther = await FindUserAsync(other.Id);
            Assert.Equal("Bếp nhà", savedOwner?.DisplayName);
            Assert.Equal("Người khác", savedOther?.DisplayName);
        }

        [Fact]
        public async Task ChangePassword_requires_the_current_password()
        {
            var user = await CreateUserAsync("Đầu bếp");
            using var client = _factory.CreateTestClient(user.Id);

            var rejected = await PostAsync(
                client,
                "/Account/ChangePassword",
                await GetTokenAsync(client, "/Account/Settings"),
                new Dictionary<string, string>
                {
                    ["PasswordForm.CurrentPassword"] = "WrongPassword123!",
                    ["PasswordForm.NewPassword"] = "NewPassword123!",
                    ["PasswordForm.ConfirmPassword"] = "NewPassword123!"
                });

            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            Assert.True(await CheckPasswordAsync(user.Id, Password));

            var accepted = await PostAsync(
                client,
                "/Account/ChangePassword",
                await GetTokenAsync(client, "/Account/Settings"),
                new Dictionary<string, string>
                {
                    ["PasswordForm.CurrentPassword"] = Password,
                    ["PasswordForm.NewPassword"] = "NewPassword123!",
                    ["PasswordForm.ConfirmPassword"] = "NewPassword123!"
                });

            Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
            Assert.True(
                await CheckPasswordAsync(user.Id, "NewPassword123!"));
            Assert.False(await CheckPasswordAsync(user.Id, Password));
        }

        [Fact]
        public async Task ChangeEmail_waits_for_confirmation_of_the_new_address()
        {
            var user = await CreateUserAsync("Đầu bếp");
            var newEmail = UniqueEmail();
            using var client = _factory.CreateTestClient(user.Id);

            var response = await PostAsync(
                client,
                "/Account/ChangeEmail",
                await GetTokenAsync(client, "/Account/Settings"),
                new Dictionary<string, string>
                {
                    ["EmailForm.NewEmail"] = newEmail,
                    ["EmailForm.CurrentPassword"] = Password
                });

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(user.Email, (await FindUserAsync(user.Id))?.Email);

            var link = LinkFor(newEmail);
            var confirmed = await client.GetAsync(link);

            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
            var saved = await FindUserAsync(user.Id);
            Assert.Equal(
                newEmail,
                saved?.Email,
                ignoreCase: true);
            Assert.True(saved?.EmailConfirmed);
        }

        [Fact]
        public async Task Email_confirmation_marks_the_address_verified()
        {
            var user = await CreateUserAsync("Đầu bếp");
            using var client = _factory.CreateTestClient(user.Id);

            var response = await PostAsync(
                client,
                "/Account/SendEmailConfirmation",
                await GetTokenAsync(client, "/Account/Settings"),
                new Dictionary<string, string>());

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var confirmed = await client.GetAsync(
                LinkFor(user.Email!));

            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
            Assert.True((await FindUserAsync(user.Id))?.EmailConfirmed);
        }

        [Fact]
        public async Task ForgotPassword_does_not_reveal_whether_the_account_exists()
        {
            var user = await CreateUserAsync("Đầu bếp");
            using var client = _factory.CreateTestClient();
            var unknown = UniqueEmail();

            var missing = await ForgotPasswordAsync(client, unknown);
            var known = await ForgotPasswordAsync(client, user.Email!);

            Assert.Equal(missing, known);
            Assert.Contains(
                AccountController.PasswordResetNotice,
                known,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                user.Email!,
                known,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("code=", known, StringComparison.Ordinal);
            Assert.Single(
                _factory.Emails.Messages,
                message => message.Email == user.Email);
            Assert.DoesNotContain(
                _factory.Emails.Messages,
                message => message.Email == unknown);
        }

        [Fact]
        public async Task ResetPassword_accepts_a_limited_token_and_rejects_a_bad_one()
        {
            var user = await CreateUserAsync("Đầu bếp");
            using var client = _factory.CreateTestClient();
            await ForgotPasswordAsync(client, user.Email!);

            var rejected = await ResetAsync(client, user.Id, "bad-token");
            var rejectedHtml = await rejected.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            Assert.Contains(
                "Liên kết không hợp lệ hoặc đã hết hạn.",
                rejectedHtml,
                StringComparison.Ordinal);
            Assert.True(await CheckPasswordAsync(user.Id, Password));

            var link = LinkFor(user.Email!);
            var form = await client.GetAsync(link);
            var html = await form.Content.ReadAsStringAsync();
            var accepted = await PostAsync(
                client,
                "/Account/ResetPassword",
                TokenFrom(html),
                new Dictionary<string, string>
                {
                    ["UserId"] = user.Id,
                    ["Code"] = HiddenValue(html, "Code"),
                    ["Password"] = "NewPassword123!",
                    ["ConfirmPassword"] = "NewPassword123!"
                });

            Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
            Assert.True(
                await CheckPasswordAsync(user.Id, "NewPassword123!"));
        }

        [Fact]
        public async Task DeleteAccount_removes_owned_data_and_keeps_other_users()
        {
            var owner = await CreateUserAsync("Chủ tài khoản");
            var other = await CreateUserAsync("Người khác");
            var ownedRecipeId = await AddRecipeAsync(owner.Id, "Món của tôi");
            var otherRecipeId = await AddRecipeAsync(other.Id, "Món của bạn");
            await AddInteractionAsync(owner.Id, other.Id, otherRecipeId);

            using var client = _factory.CreateTestClient(owner.Id);
            var rejected = await PostAsync(
                client,
                "/Account/DeleteAccount",
                await GetTokenAsync(client, "/Account/Settings"),
                new Dictionary<string, string>
                {
                    ["DeleteForm.Password"] = "WrongPassword123!",
                    ["DeleteForm.Confirm"] = "true"
                });

            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            Assert.NotNull(await FindUserAsync(owner.Id));

            var accepted = await PostAsync(
                client,
                "/Account/DeleteAccount",
                await GetTokenAsync(client, "/Account/Settings"),
                new Dictionary<string, string>
                {
                    ["DeleteForm.Password"] = Password,
                    ["DeleteForm.Confirm"] = "true"
                });

            Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            Assert.Null(await FindUserAsync(owner.Id));
            Assert.NotNull(await FindUserAsync(other.Id));
            Assert.False(await db.Recipes.AnyAsync(
                recipe => recipe.Id == ownedRecipeId));
            Assert.True(await db.Recipes.AnyAsync(
                recipe => recipe.Id == otherRecipeId));
            Assert.False(await db.RecipeLikes.AnyAsync(
                like => like.UserId == owner.Id));
            Assert.False(await db.RecipeComments.AnyAsync(
                comment => comment.UserId == owner.Id));
            Assert.False(await db.Follows.AnyAsync(follow =>
                follow.FollowerId == owner.Id ||
                follow.FollowingId == owner.Id));
        }

        private async Task<string> ForgotPasswordAsync(
            HttpClient client,
            string email)
        {
            var response = await PostAsync(
                client,
                "/Account/ForgotPassword",
                await GetTokenAsync(client, "/Account/ForgotPassword"),
                new Dictionary<string, string>
                {
                    ["Email"] = email
                });

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var page = await client.GetAsync(
                response.Headers.Location);
            return await page.Content.ReadAsStringAsync();
        }

        private async Task<HttpResponseMessage> ResetAsync(
            HttpClient client,
            string userId,
            string code)
        {
            var page = await client.GetAsync(
                "/Account/ResetPassword?userId=" +
                Uri.EscapeDataString(userId) +
                "&code=" +
                Uri.EscapeDataString(code));
            var html = await page.Content.ReadAsStringAsync();

            return await PostAsync(
                client,
                "/Account/ResetPassword",
                TokenFrom(html),
                new Dictionary<string, string>
                {
                    ["UserId"] = userId,
                    ["Code"] = code,
                    ["Password"] = "NewPassword123!",
                    ["ConfirmPassword"] = "NewPassword123!"
                });
        }

        private string LinkFor(string email)
        {
            var message = Assert.Single(
                _factory.Emails.Messages,
                item => item.Email == email);
            var html = WebUtility.HtmlDecode(message.HtmlBody);
            var match = Regex.Match(
                html,
                "href=\"([^\"]+)\"",
                RegexOptions.IgnoreCase);

            Assert.True(match.Success);
            return match.Groups[1].Value;
        }

        private async Task<ApplicationUser> CreateUserAsync(
            string displayName)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var suffix = Guid.NewGuid().ToString("N")[..12];
            var user = new ApplicationUser
            {
                UserName = "acct_" + suffix,
                Email = suffix + "@example.com",
                DisplayName = displayName
            };

            var result = await users.CreateAsync(user, Password);
            Assert.True(
                result.Succeeded,
                string.Join(
                    "; ",
                    result.Errors.Select(error => error.Description)));

            return user;
        }

        private async Task<ApplicationUser?> FindUserAsync(string userId)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            return await users.FindByIdAsync(userId);
        }

        private async Task<bool> CheckPasswordAsync(
            string userId,
            string password)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(userId);

            return user != null &&
                await users.CheckPasswordAsync(user, password);
        }

        private async Task<int> AddRecipeAsync(
            string userId,
            string name)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            var recipe = new Recipe
            {
                Name = name,
                Description = "Mo ta",
                Ingredients = "Rau",
                Instructions = "Nau",
                Category = "Món Việt",
                UserId = userId
            };

            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            return recipe.Id;
        }

        private async Task AddInteractionAsync(
            string ownerId,
            string otherId,
            int recipeId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            db.RecipeLikes.Add(new RecipeLike
            {
                UserId = ownerId,
                RecipeId = recipeId
            });
            db.RecipeComments.Add(new RecipeComment
            {
                UserId = ownerId,
                RecipeId = recipeId,
                Content = "Ngon"
            });
            db.Follows.Add(new Follow
            {
                FollowerId = ownerId,
                FollowingId = otherId
            });

            await db.SaveChangesAsync();
        }

        private static async Task<string> GetTokenAsync(
            HttpClient client,
            string path)
        {
            using var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            return TokenFrom(html);
        }

        private static string TokenFrom(string html)
        {
            return HiddenValue(html, "__RequestVerificationToken");
        }

        private static string HiddenValue(string html, string name)
        {
            var inputs = Regex.Matches(
                html,
                "<input\\b[^>]*>",
                RegexOptions.IgnoreCase);

            foreach (Match input in inputs)
            {
                if (!input.Value.Contains(
                        "name=\"" + name + "\"",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var value = Regex.Match(
                    input.Value,
                    "value=\"([^\"]*)\"",
                    RegexOptions.IgnoreCase);

                if (value.Success)
                {
                    return WebUtility.HtmlDecode(value.Groups[1].Value);
                }
            }

            throw new InvalidOperationException(
                "Không tìm thấy " + name + ".");
        }

        private static async Task<HttpResponseMessage> PostAsync(
            HttpClient client,
            string url,
            string token,
            Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = token;

            return await client.PostAsync(
                url,
                new FormUrlEncodedContent(fields));
        }

        private static string UniqueEmail()
        {
            return Guid.NewGuid().ToString("N")[..12] + "@example.com";
        }
    }
}
