using CookShare.Data;
using CookShare.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CookShare.Tests.Integration
{
    public class AccountSettingsWebApplicationFactory
        : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private const string Scheme = "AccountSettingsTestAuth";
        private const string HeaderScheme = "RecipeTestAuth";
        private readonly SqliteConnection _sqlite = new("Data Source=:memory:;Foreign Keys=True");
        private readonly string _databaseName = "cookshare_test_account_" + Guid.NewGuid().ToString("N");
        private readonly string? _postgresAdminConnection;
        private string? _postgresConnection;
        private bool _databaseCreated;

        public AccountSettingsWebApplicationFactory() : this(true)
        {
        }

        protected AccountSettingsWebApplicationFactory(bool usePostgreSql)
        {
            _postgresAdminConnection = usePostgreSql ? LocalPostgresConnection() : null;
        }

        public RecordingAccountEmailSender Emails
            => Services.GetRequiredService<RecordingAccountEmailSender>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:PublicBaseUrl"] = "https://localhost"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                // Exercise cookie invalidation immediately without changing production policy.
                services.Configure<SecurityStampValidatorOptions>(options =>
                    options.ValidationInterval = TimeSpan.Zero);
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    if (_postgresConnection != null)
                    {
                        options.UseNpgsql(_postgresConnection);
                    }
                    else
                    {
                        options.UseSqlite(_sqlite);
                    }
                });

                services.RemoveAll<IAccountEmailSender>();
                services.AddSingleton<RecordingAccountEmailSender>();
                services.AddSingleton<IAccountEmailSender>(provider =>
                    provider.GetRequiredService<RecordingAccountEmailSender>());

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, RecipeTestAuthHandler>(HeaderScheme, _ => { })
                    .AddPolicyScheme(Scheme, Scheme, options =>
                    {
                        options.ForwardDefaultSelector = context =>
                            context.Request.Headers.ContainsKey("X-Test-UserId")
                                ? HeaderScheme
                                : IdentityConstants.ApplicationScheme;
                    });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = Scheme;
                    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultForbidScheme = IdentityConstants.ApplicationScheme;
                });
            });
        }

        public async Task InitializeAsync()
        {
            if (_postgresAdminConnection != null)
            {
                await using var admin = new NpgsqlConnection(_postgresAdminConnection);
                await admin.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
                await create.ExecuteNonQueryAsync();
                _databaseCreated = true;
                var connection = new NpgsqlConnectionStringBuilder(_postgresAdminConnection)
                {
                    Database = _databaseName
                };
                _postgresConnection = connection.ConnectionString;
            }
            else
            {
                await _sqlite.OpenAsync();
            }

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (_postgresConnection != null)
            {
                await db.Database.MigrateAsync();
            }
            else
            {
                await db.Database.EnsureCreatedAsync();
            }
        }

        public HttpClient CreateTestClient(string? userId = null, bool useCookieAuthentication = false)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
                BaseAddress = new Uri("https://localhost")
            });
            if (!useCookieAuthentication && !string.IsNullOrEmpty(userId))
            {
                client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
            }

            return client;
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _sqlite.DisposeAsync();
            if (_databaseCreated)
            {
                // This generated name belongs only to this fixture, never to application data.
                await using var admin = new NpgsqlConnection(_postgresAdminConnection);
                await admin.OpenAsync();
                await using var drop = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)", admin);
                await drop.ExecuteNonQueryAsync();
                _databaseCreated = false;
            }
        }

        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();

        private static string? LocalPostgresConnection()
        {
            if (!PostgresFactAttribute.IsEnabled)
            {
                return null;
            }

            var connection = new NpgsqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable("COOKSHARE_TEST_CONNECTION"));
            if (connection.Host is not ("localhost" or "127.0.0.1" or "::1")
                || (connection.Database != "cookshare_test"
                    && connection.Database?.StartsWith("cookshare_test_", StringComparison.Ordinal) != true))
            {
                throw new InvalidOperationException(
                    "Account integration tests require a loopback PostgreSQL host and a disposable cookshare_test database.");
            }

            return connection.ConnectionString;
        }
    }
}
