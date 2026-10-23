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
    [Collection(CookShareDatabaseCollection.Name)]
    public class AccountSettingsIntegrationTests
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
        public async Task Settings_reads_profile_fields_without_changing_them()
        {
            var owner = await CreateUserAsync("Bếp nhà");
            var other = await CreateUserAsync("Người khác");
            await SetProfileAsync(
                owner.Id, "Bếp nhà", "/img/owner-avatar.png");
            await SetProfileAsync(
                other.Id, "Người khác", "/img/other-avatar.png");

            using var client = _factory.CreateTestClient(owner.Id);
            var page = await client.GetAsync("/Account/Settings");
            var html = await page.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Contains(
                "/Profile/Edit", html, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "UpdateDisplayName", html, StringComparison.Ordinal);
            Assert.Contains(
                "Bếp nhà", VisibleText(html), StringComparison.Ordinal);

            var changed = await PostAsync(
                client,
                "/Account/ChangePassword",
                TokenFrom(html),
                new Dictionary<string, string>
                {
                    ["PasswordForm.CurrentPassword"] = Password,
                    ["PasswordForm.NewPassword"] = "NewPassword123!",
                    ["PasswordForm.ConfirmPassword"] = "NewPassword123!"
                });

            Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
            await AssertProfileUnchangedAsync(
                owner.Id, "Bếp nhà", "/img/owner-avatar.png");
            await AssertProfileUnchangedAsync(
                other.Id, "Người khác", "/img/other-avatar.png");
            Assert.True(await CheckPasswordAsync(other.Id, Password));
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
            Assert.Equal("Đầu bếp", saved?.DisplayName);
            Assert.Null(saved?.AvatarUrl);
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
                VisibleText(known),
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                user.Email!,
                VisibleText(known),
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "code=",
                VisibleText(known),
                StringComparison.Ordinal);
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
                VisibleText(rejectedHtml),
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
        public async Task Invalid_tokens_do_not_change_email_or_password()
        {
            var user = await CreateUserAsync("Đầu bếp");
            var other = await CreateUserAsync("Người khác");
            var newEmail = UniqueEmail();
            using var client = _factory.CreateTestClient(user.Id);

            var confirm = await client.GetAsync(
                "/Account/ConfirmEmail?userId=" +
                Uri.EscapeDataString(user.Id) +
                "&code=" +
                Uri.EscapeDataString("not-a-token"));
            Assert.Contains(
                "Liên kết không hợp lệ hoặc đã hết hạn.",
                VisibleText(await confirm.Content.ReadAsStringAsync()),
                StringComparison.Ordinal);
            Assert.False((await FindUserAsync(user.Id))?.EmailConfirmed);

            var requested = await PostAsync(
                client,
                "/Account/ChangeEmail",
                await GetTokenAsync(client, "/Account/Settings"),
                new Dictionary<string, string>
                {
                    ["EmailForm.NewEmail"] = newEmail,
                    ["EmailForm.CurrentPassword"] = Password
                });
            Assert.Equal(HttpStatusCode.Redirect, requested.StatusCode);

            var change = await client.GetAsync(
                "/Account/ConfirmEmailChange?userId=" +
                Uri.EscapeDataString(user.Id) +
                "&email=" +
                Uri.EscapeDataString(newEmail) +
                "&code=" +
                Uri.EscapeDataString("not-a-token"));
            Assert.Contains(
                "Liên kết không hợp lệ hoặc đã hết hạn.",
                VisibleText(await change.Content.ReadAsStringAsync()),
                StringComparison.Ordinal);
            Assert.Equal(
                user.Email,
                (await FindUserAsync(user.Id))?.Email,
                ignoreCase: true);

            await ForgotPasswordAsync(client, user.Email!);
            var code = QueryValue(LinkFor(user.Email!), "code");
            var crossed = await ResetAsync(client, other.Id, code);
            Assert.Contains(
                "Liên kết không hợp lệ hoặc đã hết hạn.",
                VisibleText(await crossed.Content.ReadAsStringAsync()),
                StringComparison.Ordinal);
            Assert.True(await CheckPasswordAsync(user.Id, Password));
            Assert.True(await CheckPasswordAsync(other.Id, Password));
            Assert.Equal("Đầu bếp", (await FindUserAsync(user.Id))?.DisplayName);
            Assert.Equal(
                "Người khác",
                (await FindUserAsync(other.Id))?.DisplayName);
        }

        [Fact]
        public async Task Expired_tokens_do_not_change_email_or_password()
        {
            var user = await CreateUserAsync("Đầu bếp");
            var newEmail = UniqueEmail();
            using var client = _factory.CreateTestClient(user.Id);

            await PostAsync(
                client,
                "/Account/SendEmailConfirmation",
                await GetTokenAsync(client, "/Account/Settings"),
                new Dictionary<string, string>());
            var confirmLink = LinkFor(user.Email!);

            _factory.Emails.Messages.Clear();
            await PostAsync(
                client,
                "/Account/ChangeEmail",
                await GetTokenAsync(client, "/Account/Settings"),
                new Dictionary<string, string>
                {
                    ["EmailForm.NewEmail"] = newEmail,
                    ["EmailForm.CurrentPassword"] = Password
                });
            var changeLink = LinkFor(newEmail);

            _factory.Emails.Messages.Clear();
            await ForgotPasswordAsync(client, user.Email!);
            var resetCode = QueryValue(LinkFor(user.Email!), "code");

            await UsingTokenLifespanAsync(TimeSpan.FromTicks(-1), async () =>
            {
                var confirmed = await client.GetAsync(confirmLink);
                var changed = await client.GetAsync(changeLink);
                var reset = await ResetAsync(client, user.Id, resetCode);

                Assert.Contains(
                    "Liên kết không hợp lệ hoặc đã hết hạn.",
                    VisibleText(await confirmed.Content.ReadAsStringAsync()),
                    StringComparison.Ordinal);
                Assert.Contains(
                    "Liên kết không hợp lệ hoặc đã hết hạn.",
                    VisibleText(await changed.Content.ReadAsStringAsync()),
                    StringComparison.Ordinal);
                Assert.Contains(
                    "Liên kết không hợp lệ hoặc đã hết hạn.",
                    VisibleText(await reset.Content.ReadAsStringAsync()),
                    StringComparison.Ordinal);
            });

            var saved = await FindUserAsync(user.Id);
            Assert.False(saved?.EmailConfirmed);
            Assert.Equal(user.Email, saved?.Email, ignoreCase: true);
            Assert.Equal("Đầu bếp", saved?.DisplayName);
            Assert.True(await CheckPasswordAsync(user.Id, Password));
        }

        [Fact]
        public async Task DeleteAccount_removes_owned_data_and_keeps_other_users()
        {
            var owner = await CreateUserAsync("Chủ tài khoản");
            var other = await CreateUserAsync("Người khác");
            await SetProfileAsync(
                owner.Id, "Chủ tài khoản", "/img/owner-avatar.png");
            await SetProfileAsync(
                other.Id, "Người khác", "/img/other-avatar.png");
            var ownedRecipeId = await AddRecipeAsync(owner.Id, "Món của tôi");
            var otherRecipeId = await AddRecipeAsync(other.Id, "Món của bạn");
            await AddInteractionAsync(
                owner.Id, other.Id, ownedRecipeId, otherRecipeId);

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
            Assert.False(await db.Favorites.AnyAsync(
                favorite => favorite.UserId == owner.Id ||
                    favorite.RecipeId == ownedRecipeId));
            Assert.False(await db.RecipeRatings.AnyAsync(
                rating => rating.UserId == owner.Id ||
                    rating.RecipeId == ownedRecipeId));
            Assert.True(await db.Favorites.AnyAsync(favorite =>
                favorite.UserId == other.Id &&
                favorite.RecipeId == otherRecipeId));
            Assert.True(await db.RecipeRatings.AnyAsync(rating =>
                rating.UserId == other.Id &&
                rating.RecipeId == otherRecipeId &&
                rating.Stars == 5));
            await AssertProfileUnchangedAsync(
                other.Id, "Người khác", "/img/other-avatar.png");
            Assert.True(await CheckPasswordAsync(other.Id, Password));

            using var anon = _factory.CreateTestClient();
            var missing = await anon.GetAsync(
                "/Profile?userId=" + Uri.EscapeDataString(owner.Id));
            var profile = await anon.GetAsync(
                "/Profile?userId=" + Uri.EscapeDataString(other.Id));
            var profileText = VisibleText(
                await profile.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
            Assert.Contains(
                "Người khác", profileText, StringComparison.Ordinal);
            Assert.Contains(
                "/img/other-avatar.png",
                profileText,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Chủ tài khoản", profileText, StringComparison.Ordinal);
        }

        // Razor encodes characters outside Basic Latin as HTML entities.
        private static string VisibleText(string html) =>
            WebUtility.HtmlDecode(html);

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
            int ownedRecipeId,
            int otherRecipeId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            db.RecipeLikes.Add(new RecipeLike
            {
                UserId = ownerId,
                RecipeId = otherRecipeId
            });
            db.RecipeComments.Add(new RecipeComment
            {
                UserId = ownerId,
                RecipeId = otherRecipeId,
                Content = "Ngon"
            });
            db.Favorites.Add(new Favorite
            {
                UserId = ownerId,
                RecipeId = otherRecipeId
            });
            db.RecipeRatings.Add(new RecipeRating
            {
                UserId = ownerId,
                RecipeId = otherRecipeId,
                Stars = 4
            });
            db.Favorites.Add(new Favorite
            {
                UserId = otherId,
                RecipeId = ownedRecipeId
            });
            db.RecipeRatings.Add(new RecipeRating
            {
                UserId = otherId,
                RecipeId = ownedRecipeId,
                Stars = 3
            });
            db.Favorites.Add(new Favorite
            {
                UserId = otherId,
                RecipeId = otherRecipeId
            });
            db.RecipeRatings.Add(new RecipeRating
            {
                UserId = otherId,
                RecipeId = otherRecipeId,
                Stars = 5
            });
            db.Follows.Add(new Follow
            {
                FollowerId = ownerId,
                FollowingId = otherId
            });

            await db.SaveChangesAsync();
        }

        private async Task SetProfileAsync(
            string userId,
            string displayName,
            string avatarUrl)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(userId);

            Assert.NotNull(user);
            user!.DisplayName = displayName;
            user.AvatarUrl = avatarUrl;

            var result = await users.UpdateAsync(user);
            Assert.True(
                result.Succeeded,
                string.Join(
                    "; ",
                    result.Errors.Select(error => error.Description)));
        }

        private async Task AssertProfileUnchangedAsync(
            string userId,
            string displayName,
            string avatarUrl)
        {
            var user = await FindUserAsync(userId);
            Assert.Equal(displayName, user?.DisplayName);
            Assert.Equal(avatarUrl, user?.AvatarUrl);
        }

        private async Task UsingTokenLifespanAsync(
            TimeSpan lifespan,
            Func<Task> action)
        {
            using var scope = _factory.Services.CreateScope();
            var options = scope.ServiceProvider
                .GetRequiredService<
                    IOptions<DataProtectionTokenProviderOptions>>();
            var original = options.Value.TokenLifespan;
            options.Value.TokenLifespan = lifespan;

            try
            {
                await action();
            }
            finally
            {
                options.Value.TokenLifespan = original;
            }
        }

        private static string QueryValue(string url, string name)
        {
            var query = new Uri(url).Query.TrimStart('?');

            foreach (var part in query.Split(
                '&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length == 2 &&
                    string.Equals(
                        Uri.UnescapeDataString(pair[0]),
                        name,
                        StringComparison.Ordinal))
                {
                    return Uri.UnescapeDataString(pair[1]);
                }
            }

            throw new InvalidOperationException(
                "Không tìm thấy " + name + ".");
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
