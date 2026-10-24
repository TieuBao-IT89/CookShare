using System.Net;
using System.Text.RegularExpressions;
using CookShare.Controllers;
using CookShare.Models.Identity;
using CookShare.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Integration
{
    public class AccountSecurityIntegrationTests
        : IClassFixture<AccountSettingsWebApplicationFactory>
    {
        private const string Password = "TestPassword123!";
        private const string NewPassword = "NewPassword123!";
        private readonly AccountSettingsWebApplicationFactory _factory;

        public AccountSecurityIntegrationTests(AccountSettingsWebApplicationFactory factory)
        {
            _factory = factory;
            _factory.Emails.Messages.Clear();
        }

        [Theory]
        [InlineData("/Account/ChangeEmail")]
        [InlineData("/Account/ChangePassword")]
        [InlineData("/Account/SendEmailConfirmation")]
        [InlineData("/Account/DeleteAccount")]
        public async Task Sensitive_operations_require_authentication_and_antiforgery(string path)
        {
            var owner = await CreateUserAsync();
            using var anonymous = _factory.CreateTestClient();
            using var authenticated = _factory.CreateTestClient(owner.Id);

            var anonymousResponse = await anonymous.PostAsync(path, new FormUrlEncodedContent([]));
            var authenticatedResponse = await authenticated.PostAsync(path, new FormUrlEncodedContent([]));

            Assert.Equal(HttpStatusCode.Redirect, anonymousResponse.StatusCode);
            Assert.StartsWith("https://localhost/Account/Login", anonymousResponse.Headers.Location?.ToString());
            Assert.Equal(HttpStatusCode.BadRequest, authenticatedResponse.StatusCode);
            await AssertProfileAsync(owner.Id, owner.Email!, confirmed: false);
            Assert.Empty(_factory.Emails.Messages);
        }

        [Theory]
        [InlineData("short", "short")]
        [InlineData("longlowercase", "longlowercase")]
        [InlineData(NewPassword, "DifferentPassword123!")]
        public async Task Password_rules_and_confirmation_are_enforced(string password, string confirmation)
        {
            var owner = await CreateUserAsync();
            using var client = _factory.CreateTestClient(owner.Id);

            var response = await PostAsync(client, "/Account/ChangePassword", "/Account/Settings", new()
            {
                ["PasswordForm.CurrentPassword"] = Password,
                ["PasswordForm.NewPassword"] = password,
                ["PasswordForm.ConfirmPassword"] = confirmation
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("text-danger", await response.Content.ReadAsStringAsync());
            Assert.True(await HasPasswordAsync(owner.Id, Password));
            await AssertProfileAsync(owner.Id, owner.Email!, confirmed: false);
        }

        [Fact]
        public async Task Change_email_rejects_wrong_password_and_existing_address()
        {
            var owner = await CreateUserAsync();
            var other = await CreateUserAsync();
            using var client = _factory.CreateTestClient(owner.Id);

            var wrongPassword = await PostAsync(client, "/Account/ChangeEmail", "/Account/Settings", new()
            {
                ["EmailForm.NewEmail"] = "new-" + owner.Email,
                ["EmailForm.CurrentPassword"] = "WrongPassword123!"
            });
            var duplicateEmail = await PostAsync(client, "/Account/ChangeEmail", "/Account/Settings", new()
            {
                ["EmailForm.NewEmail"] = other.Email!,
                ["EmailForm.CurrentPassword"] = Password
            });

            Assert.Contains("Mật khẩu hiện tại không chính xác.", await TextAsync(wrongPassword));
            Assert.Contains("Email này đã được sử dụng.", await TextAsync(duplicateEmail));
            Assert.Empty(_factory.Emails.Messages);
            await AssertProfileAsync(owner.Id, owner.Email!, confirmed: false);
            await AssertProfileAsync(other.Id, other.Email!, confirmed: false);
        }

        [Fact]
        public async Task Email_change_token_is_bound_to_the_user_and_new_address()
        {
            var owner = await CreateUserAsync();
            var other = await CreateUserAsync();
            var newEmail = "new-" + owner.Email;
            using var client = _factory.CreateTestClient(owner.Id);

            var requested = await PostAsync(client, "/Account/ChangeEmail", "/Account/Settings", new()
            {
                ["EmailForm.NewEmail"] = newEmail,
                ["EmailForm.CurrentPassword"] = Password,
                ["UserId"] = other.Id
            });
            Assert.Equal(HttpStatusCode.Redirect, requested.StatusCode);
            await AssertProfileAsync(owner.Id, owner.Email!, confirmed: false);

            var link = LinkFor(newEmail);
            var code = QueryHelpers.ParseQuery(link.Query)["code"].ToString();
            var crossedUser = await client.GetAsync(QueryHelpers.AddQueryString("/Account/ConfirmEmailChange", new Dictionary<string, string?>
            {
                ["userId"] = other.Id,
                ["email"] = newEmail,
                ["code"] = code
            }));
            var crossedEmail = await client.GetAsync(QueryHelpers.AddQueryString("/Account/ConfirmEmailChange", new Dictionary<string, string?>
            {
                ["userId"] = owner.Id,
                ["email"] = "tampered-" + newEmail,
                ["code"] = code
            }));

            Assert.Contains("Liên kết không hợp lệ hoặc đã hết hạn.", await TextAsync(crossedUser));
            Assert.Contains("Liên kết không hợp lệ hoặc đã hết hạn.", await TextAsync(crossedEmail));
            await AssertProfileAsync(owner.Id, owner.Email!, confirmed: false);
            await AssertProfileAsync(other.Id, other.Email!, confirmed: false);

            var confirmed = await client.GetAsync(link);
            Assert.Contains("Đã đổi email", await TextAsync(confirmed));
            await AssertProfileAsync(owner.Id, newEmail, confirmed: true);
            await AssertProfileAsync(other.Id, other.Email!, confirmed: false);

            var replayed = await client.GetAsync(link);
            Assert.Contains("Liên kết không hợp lệ hoặc đã hết hạn.", await TextAsync(replayed));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Password_reset_is_single_use_and_preserves_profile_and_verification(bool confirmed)
        {
            var owner = await CreateUserAsync(confirmed);
            var code = await ResetCodeAsync(owner.Id);
            using var client = _factory.CreateTestClient();

            var accepted = await ResetAsync(client, owner.Id, code, NewPassword);
            Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
            Assert.True(await HasPasswordAsync(owner.Id, NewPassword));
            Assert.False(await HasPasswordAsync(owner.Id, Password));
            await AssertProfileAsync(owner.Id, owner.Email!, confirmed);

            var replayed = await ResetAsync(client, owner.Id, code, "ReplayedPassword123!");
            Assert.Contains("Liên kết không hợp lệ hoặc đã hết hạn.", await TextAsync(replayed));
            Assert.True(await HasPasswordAsync(owner.Id, NewPassword));
        }

        [Fact]
        public async Task Email_links_use_the_configured_origin_despite_host_headers()
        {
            var owner = await CreateUserAsync();
            using var client = _factory.CreateTestClient();
            client.DefaultRequestHeaders.Host = "attacker.example";
            client.DefaultRequestHeaders.Add("X-Forwarded-Host", "attacker.example");

            var response = await PostAsync(client, "/Account/ForgotPassword", "/Account/ForgotPassword", new()
            {
                ["Email"] = owner.Email!
            });

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var link = LinkFor(owner.Email!);
            Assert.Equal("https", link.Scheme);
            Assert.Equal("localhost", link.Host);
            Assert.DoesNotContain("attacker.example", link.AbsoluteUri);
        }

        [Theory]
        [InlineData("")]
        [InlineData("http://cookshare.example")]
        [InlineData("https://user:password@cookshare.example")]
        [InlineData("https://cookshare.example?redirect=attacker")]
        public async Task Unsafe_production_email_base_urls_do_not_send_tokens(string publicBaseUrl)
        {
            var owner = await CreateUserAsync();
            using var application = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureTestServices(services =>
                    services.PostConfigure<EmailOptions>(options => options.PublicBaseUrl = publicBaseUrl));
            });
            using var client = application.CreateClient(new()
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

            var response = await PostAsync(client, "/Account/ForgotPassword", "/Account/ForgotPassword", new()
            {
                ["Email"] = owner.Email!
            });

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var emails = application.Services.GetRequiredService<RecordingAccountEmailSender>();
            Assert.Empty(emails.Messages);
            var notice = await client.GetAsync(response.Headers.Location);
            Assert.Contains(AccountController.PasswordResetNotice, await TextAsync(notice));
        }

        [Fact]
        public async Task Failed_email_delivery_reports_errors_without_exposing_account_existence()
        {
            var owner = await CreateUserAsync();
            using var client = _factory.CreateTestClient(owner.Id);
            _factory.Emails.FailDelivery = true;
            try
            {
                var verification = await PostAsync(client, "/Account/SendEmailConfirmation", "/Account/Settings", new());
                var settings = await client.GetAsync(verification.Headers.Location);
                Assert.Contains("Chưa gửi được email xác minh.", await TextAsync(settings));

                var change = await PostAsync(client, "/Account/ChangeEmail", "/Account/Settings", new()
                {
                    ["EmailForm.NewEmail"] = "new-" + owner.Email,
                    ["EmailForm.CurrentPassword"] = Password
                });
                settings = await client.GetAsync(change.Headers.Location);
                Assert.Contains("Chưa gửi được email xác nhận.", await TextAsync(settings));

                using var anonymous = _factory.CreateTestClient();
                var known = await PostAsync(anonymous, "/Account/ForgotPassword", "/Account/ForgotPassword", new()
                {
                    ["Email"] = owner.Email!
                });
                var unknown = await PostAsync(anonymous, "/Account/ForgotPassword", "/Account/ForgotPassword", new()
                {
                    ["Email"] = Guid.NewGuid().ToString("N") + "@example.com"
                });
                Assert.Equal(known.StatusCode, unknown.StatusCode);
                Assert.Equal(known.Headers.Location, unknown.Headers.Location);
                var notice = await anonymous.GetAsync(known.Headers.Location);
                var text = await TextAsync(notice);
                Assert.Contains(AccountController.PasswordResetNotice, text);
                Assert.DoesNotContain("Simulated", text);
                Assert.DoesNotContain("code=", text);
                Assert.Empty(_factory.Emails.Messages);
                await AssertProfileAsync(owner.Id, owner.Email!, confirmed: false);
            }
            finally
            {
                _factory.Emails.FailDelivery = false;
            }
        }

        [Fact]
        public async Task Password_change_refreshes_current_cookie_and_revokes_other_sessions()
        {
            var owner = await CreateUserAsync();
            using var current = _factory.CreateTestClient(useCookieAuthentication: true);
            using var otherSession = _factory.CreateTestClient(useCookieAuthentication: true);
            await LoginAsync(current, owner.Email!, Password);
            await LoginAsync(otherSession, owner.Email!, Password);

            var response = await PostAsync(current, "/Account/ChangePassword", "/Account/Settings", new()
            {
                ["PasswordForm.CurrentPassword"] = Password,
                ["PasswordForm.NewPassword"] = NewPassword,
                ["PasswordForm.ConfirmPassword"] = NewPassword
            });

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await current.GetAsync("/Account/Settings")).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await otherSession.GetAsync("/Account/Settings")).StatusCode);
            await AssertProfileAsync(owner.Id, owner.Email!, confirmed: false);
        }

        [Fact]
        public async Task Password_reset_revokes_existing_cookie_and_allows_login_with_new_password()
        {
            var owner = await CreateUserAsync();
            using var existingSession = _factory.CreateTestClient(useCookieAuthentication: true);
            using var anonymous = _factory.CreateTestClient(useCookieAuthentication: true);
            await LoginAsync(existingSession, owner.Email!, Password);

            var response = await ResetAsync(anonymous, owner.Id, await ResetCodeAsync(owner.Id), NewPassword);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await existingSession.GetAsync("/Account/Settings")).StatusCode);
            await LoginAsync(anonymous, owner.Email!, NewPassword);
            Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/Account/Settings")).StatusCode);
        }

        private async Task<ApplicationUser> CreateUserAsync(bool confirmed = false)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var suffix = Guid.NewGuid().ToString("N");
            var owner = new ApplicationUser
            {
                UserName = "security_" + suffix,
                Email = suffix + "@example.com",
                EmailConfirmed = confirmed,
                DisplayName = "Tên hiển thị được giữ lại",
                AvatarUrl = "/img/security-avatar.png"
            };
            Assert.True((await users.CreateAsync(owner, Password)).Succeeded);
            return owner;
        }

        private async Task AssertProfileAsync(string userId, string email, bool confirmed)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var saved = await users.FindByIdAsync(userId);
            Assert.NotNull(saved);
            Assert.Equal(email, saved.Email);
            Assert.Equal(confirmed, saved.EmailConfirmed);
            Assert.Equal("Tên hiển thị được giữ lại", saved.DisplayName);
            Assert.Equal("/img/security-avatar.png", saved.AvatarUrl);
        }

        private async Task<bool> HasPasswordAsync(string userId, string password)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(userId);
            return user != null && await users.CheckPasswordAsync(user, password);
        }

        private async Task<string> ResetCodeAsync(string userId)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(userId);
            return await users.GeneratePasswordResetTokenAsync(user!);
        }

        private static async Task<HttpResponseMessage> ResetAsync(HttpClient client, string userId, string code, string password)
        {
            var formUrl = QueryHelpers.AddQueryString("/Account/ResetPassword", new Dictionary<string, string?>
            {
                ["userId"] = userId,
                ["code"] = code
            });
            return await PostAsync(client, "/Account/ResetPassword", formUrl, new()
            {
                ["UserId"] = userId,
                ["Code"] = code,
                ["Password"] = password,
                ["ConfirmPassword"] = password
            });
        }

        private static async Task LoginAsync(HttpClient client, string email, string password)
        {
            var response = await PostAsync(client, "/Account/Login", "/Account/Login", new()
            {
                ["UserNameOrEmail"] = email,
                ["Password"] = password
            });
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Settings")).StatusCode);
        }

        private Uri LinkFor(string email)
        {
            var message = Assert.Single(_factory.Emails.Messages, item => item.Email == email);
            var match = Regex.Match(WebUtility.HtmlDecode(message.HtmlBody), "href=\"([^\"]+)\"");
            Assert.True(match.Success);
            return new Uri(match.Groups[1].Value);
        }

        private static async Task<string> TextAsync(HttpResponseMessage response)
        {
            return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        }

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string action, string formPage, Dictionary<string, string> fields)
        {
            var form = await client.GetAsync(formPage);
            form.EnsureSuccessStatusCode();
            var html = await form.Content.ReadAsStringAsync();
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(token.Success);
            fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
            return await client.PostAsync(action, new FormUrlEncodedContent(fields));
        }
    }
}
