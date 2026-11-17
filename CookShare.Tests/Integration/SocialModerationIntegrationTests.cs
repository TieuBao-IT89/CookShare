using System.Net;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.Services;
using CookShare.Tests.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CookShare.Tests.Integration
{
    public sealed class SocialModerationIntegrationTests : IAsyncLifetime
    {
        private const string Password = "SocialTests123!";
        private readonly SocialFactory _factory = new();
        private int _recipeId;
        private int _postId;
        private int _reportId;
        private string _fileName = string.Empty;

        public async Task InitializeAsync()
        {
            await _factory.InitializeAsync();
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new IdentityRole("Moderator"))).Succeeded);
            foreach (string id in new[] { "owner", "cook", "moderator" })
            {
                var user = new ApplicationUser { Id = id, UserName = id, Email = id + "@example.test", DisplayName = id };
                Assert.True((await users.CreateAsync(user, Password)).Succeeded);
                if (id == "moderator")
                {
                    Assert.True((await users.AddToRoleAsync(user, "Moderator")).Succeeded);
                }
            }
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var recipe = new Recipe
            {
                Name = "Món thử kiểm duyệt",
                Category = "Món Việt",
                Description = "Một món ngon",
                Ingredients = "Rau",
                Instructions = "Nấu chín",
                UserId = "owner"
            };
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            _recipeId = recipe.Id;
            _postId = await scope.ServiceProvider.GetRequiredService<CookedPostService>()
                .CreateAsync("cook", recipe.Id, "Thành phẩm của tôi", Guid.NewGuid(), SocialTestDatabase.Image());
            _fileName = await db.Set<CookedRecipePost>().Where(post => post.Id == _postId).Select(post => post.ImageFileName).SingleAsync();
            var report = new ContentReport { ReporterId = "owner", TargetType = ContentTargetType.CookedPost, TargetId = _postId, Reason = ReportReason.Spam };
            db.Set<ContentReport>().Add(report);
            await db.SaveChangesAsync();
            _reportId = report.Id;
        }

        public async Task DisposeAsync() => await _factory.DisposeAsync();

        [Fact]
        public async Task OrdinaryAuthenticatedCookie_CannotAccessModerationQueueOrDirectPost()
        {
            using var client = await LoginAsync("owner");
            using var get = await client.GetAsync("/Moderation");
            AssertDenied(get);
            using var image = await client.GetAsync("/Moderation/CookedPostImage/" + _postId);
            AssertDenied(image);
            string token = await TokenAsync(client, "/ContentReports/Create?targetType=CookedPost&targetId=" + _postId);
            using var post = await PostAsync(client, "/Moderation/Decide/" + _reportId,
                new() { ["Action"] = "Hide", ["Reason"] = "Không có quyền", ["__RequestVerificationToken"] = token });
            AssertDenied(post);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False((await db.Set<CookedRecipePost>().IgnoreQueryFilters().SingleAsync()).IsHidden);
            Assert.Empty(await db.Set<ModerationAction>().ToListAsync());
        }

        [Theory]
        [InlineData("/CookedPosts/Create")]
        [InlineData("/CookedPosts/Edit/")]
        [InlineData("/CookedPosts/Delete/")]
        [InlineData("/ContentReports/Create")]
        [InlineData("/Moderation/Decide/")]
        public async Task AuthenticatedMutations_RejectMissingAntiforgeryToken(string path)
        {
            using var client = await LoginAsync("moderator");
            string endpoint = path.EndsWith('/') ? path + (path.Contains("Moderation", StringComparison.Ordinal) ? _reportId : _postId) : path;
            using var response = await PostAsync(client, endpoint, new()
            {
                ["RecipeId"] = _recipeId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Action"] = "Hide",
                ["Reason"] = "Kiểm tra"
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task TrustedModeratorCookie_HidesAndRestoresPostAndMedia_AndRecordsActor()
        {
            using var client = await LoginAsync("moderator");
            using var ordinary = _factory.CreateTestClient("cook");
            string details = "/CookedPosts/Details/" + _postId;
            string media = $"/media/cooked-posts/{_postId}/{_fileName}";
            using (var image = await ordinary.GetAsync(media))
            {
                Assert.Equal(HttpStatusCode.OK, image.StatusCode);
                Assert.True(image.Headers.CacheControl?.NoStore);
            }
            await DecideAsync(client, "Hide");
            using (var reviewImage = await client.GetAsync("/Moderation/CookedPostImage/" + _postId))
            {
                Assert.Equal(HttpStatusCode.OK, reviewImage.StatusCode);
                Assert.True(reviewImage.Headers.CacheControl?.NoStore);
            }
            using (var hiddenPost = await ordinary.GetAsync(details))
            using (var hiddenImage = await ordinary.GetAsync(media))
            {
                Assert.Equal(HttpStatusCode.NotFound, hiddenPost.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, hiddenImage.StatusCode);
            }
            await DecideAsync(client, "Restore");
            using (var restored = await ordinary.GetAsync(details))
            {
                Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
            }
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var actions = await db.Set<ModerationAction>().OrderBy(action => action.Id).ToListAsync();
            Assert.Equal(new[] { ModerationActionType.Hide, ModerationActionType.Restore }, actions.Select(action => action.Action));
            Assert.All(actions, action => Assert.Equal("moderator", action.ModeratorId));
        }

        [Fact]
        public async Task AnonymousUser_CanReadVisiblePost_ButIsChallengedForUploadAndReport()
        {
            using var client = _factory.CreateTestClient();
            using var visible = await client.GetAsync("/CookedPosts/Details/" + _postId);
            using var upload = await client.GetAsync("/CookedPosts/Create?recipeId=" + _recipeId);
            using var report = await client.GetAsync("/ContentReports/Create?targetType=CookedPost&targetId=" + _postId);
            Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
            Assert.Equal("/Account/Login", upload.Headers.Location?.AbsolutePath);
            Assert.Equal(HttpStatusCode.Redirect, report.StatusCode);
            Assert.Equal("/Account/Login", report.Headers.Location?.AbsolutePath);
        }

        private async Task DecideAsync(HttpClient client, string action)
        {
            string token = await TokenAsync(client, "/Moderation/Details/" + _reportId);
            using var response = await PostAsync(client, "/Moderation/Decide/" + _reportId,
                new() { ["Action"] = action, ["Reason"] = "Đã kiểm tra nội dung", ["__RequestVerificationToken"] = token });
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        private async Task<HttpClient> LoginAsync(string userName)
        {
            var client = _factory.CreateTestClient(useCookieAuthentication: true);
            string token = await TokenAsync(client, "/Account/Login");
            using var response = await PostAsync(client, "/Account/Login", new()
            {
                ["UserNameOrEmail"] = userName,
                ["Password"] = Password,
                ["__RequestVerificationToken"] = token
            });
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return client;
        }

        private static async Task<string> TokenAsync(HttpClient client, string path)
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string html = await response.Content.ReadAsStringAsync();
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant);
            Assert.True(match.Success, "An antiforgery token must be rendered by the real form.");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, Dictionary<string, string> fields)
            => client.PostAsync(path, new FormUrlEncodedContent(fields));

        private static void AssertDenied(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Account/AccessDenied", response.Headers.Location?.AbsolutePath);
        }

        private sealed class SocialFactory() : AccountSettingsWebApplicationFactory(false)
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ISocialImageStorage>();
                    services.AddSingleton<ISocialImageStorage, RecordingSocialImageStorage>();
                });
            }
        }
    }
}
