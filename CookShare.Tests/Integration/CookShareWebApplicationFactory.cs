
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CookShare.Data;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CookShare.Tests.Integration
{
    public class CookShareWebApplicationFactory
        : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private const string Scheme = "RecipeTestAuth";

        private string? _ownerId;
        private string? _otherUserId;

        public string OwnerId => _ownerId
            ?? throw new InvalidOperationException(
                "Test users have not been initialized.");

        public string OtherUserId => _otherUserId
            ?? throw new InvalidOperationException(
                "Test users have not been initialized.");

        protected override void ConfigureWebHost(
            Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureTestServices(services =>
            {
                var connection = Environment.GetEnvironmentVariable(
                    "COOKSHARE_TEST_CONNECTION");

                if (string.IsNullOrWhiteSpace(connection))
                {
                    throw new InvalidOperationException(
                        "COOKSHARE_TEST_CONNECTION is required.");
                }

                services.RemoveAll<
                    DbContextOptions<ApplicationDbContext>>();

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseNpgsql(connection));

                services.AddAuthentication()
                    .AddScheme<
                        AuthenticationSchemeOptions,
                        RecipeTestAuthHandler>(
                            Scheme,
                            _ => { });

                // Giữ nguyên cookie challenge cho người chưa đăng nhập.
                services.PostConfigure<AuthenticationOptions>(
                    options =>
                    {
                        options.DefaultAuthenticateScheme = Scheme;
                        options.DefaultChallengeScheme =
                            IdentityConstants.ApplicationScheme;
                        options.DefaultForbidScheme = Scheme;
                    });
            });
        }

        public async Task InitializeAsync()
        {
            // xUnit can initialize class fixtures even when every PostgreSQL
            // fact is skipped. Do not start its host or migrate without opt-in.
            if (!PostgresFactAttribute.IsEnabled)
            {
                return;
            }

            using var scope = Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            await db.Database.MigrateAsync();

            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            _ownerId = await CreateUserAsync(users);
            _otherUserId = await CreateUserAsync(users);
        }

        private static async Task<string> CreateUserAsync(
            UserManager<ApplicationUser> users)
        {
            var suffix = Guid.NewGuid().ToString("N");

            var user = new ApplicationUser
            {
                UserName = "test_" + suffix,
                Email = suffix + "@example.com",
                DisplayName = "Test User"
            };

            var result = await users.CreateAsync(
                user, "TestPassword123!");

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(
                        "; ",
                        result.Errors.Select(e => e.Description)));
            }

            return user.Id;
        }

        public HttpClient CreateTestClient(string? userId = null)
        {
            var client = CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    HandleCookies = true,
                    BaseAddress = new Uri("https://localhost")
                });

            if (!string.IsNullOrEmpty(userId))
            {
                client.DefaultRequestHeaders.Add(
                    "X-Test-UserId", userId);
            }

            return client;
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            await base.DisposeAsync();
        }
    }

    public sealed class RecipeTestAuthHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public RecipeTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult>
            HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(
                    "X-Test-UserId", out var header)
                || string.IsNullOrWhiteSpace(header.ToString()))
            {
                return Task.FromResult(
                    AuthenticateResult.NoResult());
            }

            var userId = header.ToString();

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, "TestUser")
            };

            var identity = new ClaimsIdentity(
                claims, SchemeName);

            var principal = new ClaimsPrincipal(identity);

            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(
                        principal, SchemeName)));
        }

        protected override Task HandleForbiddenAsync(
            AuthenticationProperties properties)
        {
            Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return Task.CompletedTask;
        }

        private const string SchemeName = "RecipeTestAuth";
    }
}
