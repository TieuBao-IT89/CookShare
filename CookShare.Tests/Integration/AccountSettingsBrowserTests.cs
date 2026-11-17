using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CookShare.Tests.Integration
{
    public sealed class AccountSettingsBrowserTests
    {
        private readonly ITestOutputHelper _output;

        public AccountSettingsBrowserTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [AccountSettingsBrowserFact]
        public async Task LiveBrowser_ShouldCompleteAccountWorkflowsAndResponsiveLayouts()
        {
            await using var factory = new QuietBrowserFactory();
            await factory.InitializeAsync();
            await SeedAsync(factory);
            using var upstream = factory.Server.CreateClient();
            upstream.BaseAddress = new Uri("https://localhost");
            string artifacts = Path.Combine(RepositoryRoot(), "artifacts", "issue101", "browser-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(artifacts);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            await using var proxy = builder.Build();
            var resultReceived = new TaskCompletionSource<BrowserResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            proxy.Run(context => ProxyAsync(context, factory, upstream, resultReceived));
            await proxy.StartAsync();
            string address = proxy.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            // Chrome permits Secure cookies on trustworthy HTTP localhost. The
            // application still receives HTTPS requests in TestServer and keeps
            // its normal Identity/antiforgery cookie settings.
            address = address.Replace("127.0.0.1", "localhost", StringComparison.Ordinal);

            var start = new ProcessStartInfo(AccountSettingsBrowserFactAttribute.BrowserPath()!)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string argument in new[]
            {
                "--headless=new", "--disable-gpu", "--no-sandbox", "--no-first-run", "--no-default-browser-check",
                "--disable-background-networking", "--disable-extensions",
                "--host-resolver-rules=MAP * ~NOTFOUND, EXCLUDE 127.0.0.1, EXCLUDE localhost",
                "--window-size=1600,1200", "--virtual-time-budget=60000", "--dump-dom",
                "--user-data-dir=" + Path.Combine(artifacts, "profile"), address + "/__browser"
            })
            {
                start.ArgumentList.Add(argument);
            }

            using var browser = Process.Start(start)!;
            Task<string> standardOutput = browser.StandardOutput.ReadToEndAsync();
            Task<string> standardError = browser.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            BrowserResult result;
            try
            {
                Task exit = browser.WaitForExitAsync(timeout.Token);
                Task completed = await Task.WhenAny(resultReceived.Task.WaitAsync(timeout.Token), exit);
                if (completed == exit && !resultReceived.Task.IsCompleted)
                {
                    string document = await standardOutput;
                    string diagnostics = await standardError;
                    _output.WriteLine("Browser exit code: " + browser.ExitCode);
                    _output.WriteLine("Runner status: " + WebUtility.HtmlDecode(
                        Regex.Match(document, "<pre id=\"results\">([\\s\\S]*?)</pre>").Groups[1].Value));
                    _output.WriteLine("Browser diagnostics: " + Regex.Replace(diagnostics,
                        "(?i)([?&](?:code|token)=)[^&\\s\"<>]*", "$1[redacted]"));
                }

                Assert.True(completed != exit || resultReceived.Task.IsCompleted,
                    "The browser exited before reporting a result. Check that Chrome/Edge can run headlessly.");
                result = await resultReceived.Task.WaitAsync(timeout.Token);
                await exit;
                // Never persist browser DOM or request URLs: they can contain
                // short-lived confirmation tokens during a failed workflow.
                await standardOutput;
                await standardError;
            }
            finally
            {
                if (!browser.HasExited)
                {
                    browser.Kill(entireProcessTree: true);
                    await browser.WaitForExitAsync();
                }

                await proxy.StopAsync();
                await RemoveBrowserProfileAsync(artifacts);
            }

            string summary = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(artifacts, "result.json"), summary);
            _output.WriteLine(summary);
            _output.WriteLine("Evidence: " + Path.Combine(artifacts, "result.json"));
            Assert.True(result.Passed, string.Join(Environment.NewLine, result.Checks));
            Assert.Equal(0, browser.ExitCode);
            Assert.True(result.Checks.Length >= 15, "The browser must finish the full workflow checklist.");
        }

        private static async Task ProxyAsync(HttpContext context, AccountSettingsWebApplicationFactory factory,
            HttpClient upstream, TaskCompletionSource<BrowserResult> resultReceived)
        {
            switch (context.Request.Path.Value)
            {
                case "/__browser":
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync(AccountSettingsBrowserPage.Html);
                    return;
                case "/__browser/runner.js":
                    context.Response.ContentType = "text/javascript; charset=utf-8";
                    await context.Response.WriteAsync(AccountSettingsBrowserPage.JavaScript);
                    return;
                case "/__browser/result":
                    var result = await context.Request.ReadFromJsonAsync<BrowserResult>();
                    resultReceived.TrySetResult(result!);
                    await context.Response.WriteAsync("ok");
                    return;
                case "/__browser/email":
                    string email = context.Request.Query["email"].ToString();
                    var message = factory.Emails.Messages.LastOrDefault(item => item.Email == email);
                    string? path = message == null ? null : new Uri(WebUtility.HtmlDecode(
                        Regex.Match(message.HtmlBody, "href=\"([^\"]+)\"").Groups[1].Value)).PathAndQuery;
                    await context.Response.WriteAsJsonAsync(new { path });
                    return;
                case "/__browser/state":
                    using (var scope = factory.Services.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == "browser-owner");
                        await context.Response.WriteAsJsonAsync(new
                        {
                            exists = user != null,
                            email = user?.Email,
                            emailConfirmed = user?.EmailConfirmed,
                            displayName = user?.DisplayName,
                            avatarUrl = user?.AvatarUrl,
                            unrelatedExists = await db.Users.AnyAsync(item => item.Id == "browser-other"),
                            importedExists = await db.Recipes.AnyAsync(item => item.Source == "browser-source")
                        });
                    }

                    return;
            }

            using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method),
                context.Request.Path + context.Request.QueryString);
            if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
            {
                using var buffer = new MemoryStream();
                await context.Request.Body.CopyToAsync(buffer);
                request.Content = new ByteArrayContent(buffer.ToArray());
            }

            foreach (var header in context.Request.Headers)
            {
                if (header.Key is "Host" or "Connection" or "Content-Length" or "Transfer-Encoding" or "X-Test-UserId")
                {
                    continue;
                }

                if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                {
                    request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            using var response = await upstream.SendAsync(request);
            context.Response.StatusCode = (int)response.StatusCode;
            foreach (var header in response.Headers.Concat(response.Content.Headers))
            {
                if (header.Key is not ("Transfer-Encoding" or "Connection"))
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }
            }

            if (response.Headers.Location is { IsAbsoluteUri: true, Host: "localhost" } location)
            {
                context.Response.Headers.Location = location.PathAndQuery;
            }

            await response.Content.CopyToAsync(context.Response.Body);
        }

        private static async Task RemoveBrowserProfileAsync(string artifacts)
        {
            string runDirectory = Path.GetFullPath(artifacts);
            string profile = Path.GetFullPath(Path.Combine(runDirectory, "profile"));
            if (!profile.StartsWith(runDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || Directory.GetParent(profile)?.FullName != runDirectory)
            {
                throw new InvalidOperationException("The disposable browser profile must stay inside this test's artifact directory.");
            }

            for (int attempt = 0; Directory.Exists(profile); attempt++)
            {
                try
                {
                    Directory.Delete(profile, recursive: true);
                }
                catch (IOException) when (attempt < 10)
                {
                    await Task.Delay(100);
                }
            }
        }

        private static async Task SeedAsync(AccountSettingsWebApplicationFactory factory)
        {
            using var scope = factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var owner = new ApplicationUser
            {
                Id = "browser-owner",
                UserName = "browser-cook",
                Email = "browser-cook@example.com",
                DisplayName = "Browser cook"
            };
            Assert.True((await users.CreateAsync(owner, "BrowserPassword123!")).Succeeded);
            Assert.True((await users.CreateAsync(new ApplicationUser
            {
                Id = "browser-other",
                UserName = "browser-other",
                Email = "browser-other@example.com",
                DisplayName = "Unrelated cook"
            }, "UnrelatedPassword123!")).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", db.Database.ProviderName);
            db.Recipes.Add(new Recipe
            {
                Name = "Unrelated imported recipe",
                Description = "Keep this recipe",
                Category = "Món Việt",
                Ingredients = "Rice",
                Instructions = "Cook",
                Source = "browser-source",
                ExternalId = "keep"
            });
            await db.SaveChangesAsync();
        }

        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CookShare.sln")))
                {
                    return directory.FullName;
                }
            }

            throw new InvalidOperationException("CookShare.sln was not found.");
        }

        private sealed class QuietBrowserFactory : AccountSettingsWebApplicationFactory
        {
            public QuietBrowserFactory() : base(false)
            {
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);
                builder.ConfigureLogging(logging => logging.ClearProviders());
            }
        }

        private sealed record BrowserResult(bool Passed, string[] Checks);

    }

    public sealed class AccountSettingsBrowserFactAttribute : FactAttribute
    {
        public AccountSettingsBrowserFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("COOKSHARE_RUN_BROWSER_TESTS") != "1")
            {
                Skip = "Opt in with COOKSHARE_RUN_BROWSER_TESTS=1 to run live Chrome/Edge workflows.";
            }
            else if (BrowserPath() == null)
            {
                Skip = "Chrome or Edge is required for live browser verification.";
            }
        }

        internal static string? BrowserPath()
        {
            return new[]
            {
                Environment.GetEnvironmentVariable("COOKSHARE_BROWSER_PATH"),
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                "/usr/bin/google-chrome", "/usr/bin/chromium", "/usr/bin/chromium-browser"
            }.FirstOrDefault(path => path != null && File.Exists(path));
        }
    }
}
