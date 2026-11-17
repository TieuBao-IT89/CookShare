using CookShare.Data;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CookShare.Tests.Integration
{
    public sealed class IsolatedRecipeWebApplicationFactory : WebApplicationFactory<Program>
    {
        private const string Scheme = "RecipeTestAuth";
        private readonly string _databaseName = "recipe-http-" + Guid.NewGuid();

        public string OwnerId => "owner-user";

        public string OtherUserId => "other-user";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                // Replace the complete EF options registration before any context is resolved.
                // This host never opens the configured application PostgreSQL connection.
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, RecipeTestAuthHandler>(Scheme, _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = Scheme;
                    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultForbidScheme = Scheme;
                });
            });
        }

        public async Task InitializeAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(
                new ApplicationUser
                {
                    Id = OwnerId,
                    UserName = "kitchen-owner",
                    DisplayName = "Kitchen Owner"
                },
                new ApplicationUser
                {
                    Id = OtherUserId,
                    UserName = "other-cook",
                    DisplayName = "Other Cook"
                });
            await db.SaveChangesAsync();
        }

        public HttpClient CreateTestClient(string? userId = null)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
                BaseAddress = new Uri("https://localhost")
            });
            if (!string.IsNullOrEmpty(userId))
            {
                client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
            }

            return client;
        }
    }
}
