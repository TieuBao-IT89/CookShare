using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Integration
{
    public sealed class FollowingFeedIntegrationTests : IAsyncLifetime
    {
        private readonly IsolatedRecipeWebApplicationFactory _factory = new();

        public Task InitializeAsync() => _factory.InitializeAsync();
        public async Task DisposeAsync() => await _factory.DisposeAsync();

        [Fact]
        public async Task Following_ShouldRequireLoginWhilePublicFeedRemainsAvailable()
        {
            using var guest = _factory.CreateTestClient();
            using var page = await guest.GetAsync("/Feed/Following");
            using var queryPage = await guest.GetAsync("/?feed=following");
            using var more = await guest.GetAsync("/Feed/More?feed=following");
            using var publicPage = await guest.GetAsync("/");

            Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
            Assert.Contains("/Account/Login", page.Headers.Location?.OriginalString);
            Assert.Equal(HttpStatusCode.Redirect, queryPage.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, more.StatusCode);
            publicPage.EnsureSuccessStatusCode();
            Assert.True(publicPage.Headers.CacheControl?.NoStore);
            Assert.True(more.Headers.CacheControl?.NoStore);
        }

        [Fact]
        public async Task Following_ShouldUseAuthenticatedIdentityAndStableCursorPages()
        {
            int[] expected = await SeedFollowingAsync(19);
            using var client = _factory.CreateTestClient(_factory.OwnerId);
            string html = await HtmlAsync(client, "/Feed/Following?userId=" + _factory.OtherUserId);
            Assert.Contains("Căn bếp bạn theo dõi", html);
            Assert.Contains("Kitchen Owner", html);
            var actual = PostIds(html).ToList();
            Assert.Equal(8, actual.Count);
            Assert.DoesNotContain("Own private test recipe", FeedBatch(html));
            Assert.DoesNotContain("Unfollowed test recipe", FeedBatch(html));
            string? next = NextUrl(html);
            while (!string.IsNullOrEmpty(next))
            {
                using var response = await client.GetAsync(next);
                response.EnsureSuccessStatusCode();
                Assert.True(response.Headers.CacheControl?.NoStore);
                string partial = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
                Assert.DoesNotContain("<html", partial);
                actual.AddRange(PostIds(partial));
                next = NextUrl(partial);
            }

            Assert.Equal(expected, actual);
            Assert.Equal(actual.Count, actual.Distinct().Count());
        }

        [Fact]
        public async Task Following_EmptyState_ShouldNotSilentlyIncludePublicOrOwnRecipes()
        {
            await SeedFollowingAsync(2);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Follows.RemoveRange(await db.Follows.ToListAsync());
                await db.SaveChangesAsync();
            }

            using var client = _factory.CreateTestClient(_factory.OwnerId);
            string html = await HtmlAsync(client, "/Feed/Following");
            Assert.Empty(PostIds(html));
            Assert.Contains("Kết nối thêm, căn bếp vui hơn", html);
            Assert.Contains("Khám phá những căn bếp", html);
            string publicHtml = await HtmlAsync(client, "/");
            Assert.NotEmpty(PostIds(publicHtml));
        }

        [Fact]
        public async Task UnfollowAndSessionChange_ShouldBeReflectedOnLaterRequests()
        {
            await SeedFollowingAsync(12);
            using var client = _factory.CreateTestClient(_factory.OwnerId);
            string next = NextUrl(await HtmlAsync(client, "/Feed/Following"))!;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Follows.RemoveRange(await db.Follows.ToListAsync());
                await db.SaveChangesAsync();
            }

            Assert.Empty(PostIds(await HtmlAsync(client, next)));
            using var other = _factory.CreateTestClient(_factory.OtherUserId);
            Assert.Empty(PostIds(await HtmlAsync(other, next)));
            using var guest = _factory.CreateTestClient();
            using var expired = await guest.GetAsync(next);
            Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        }

        [Theory]
        [InlineData("/?feed=unknown")]
        [InlineData("/Feed/More?feed=unknown")]
        [InlineData("/?cursor=not-valid!")]
        [InlineData("/Feed/Following?cursor=not-valid!")]
        [InlineData("/Feed/More?cursor=not-valid!")]
        public async Task InvalidFeedOrCursor_ShouldReturn400(string route)
        {
            using var client = _factory.CreateTestClient(_factory.OwnerId);
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Feed_ShouldEscapeUserContentAndSupportNativePaginationAndExistingActions()
        {
            await SeedFollowingAsync(12);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var latest = await db.Recipes.OrderByDescending(recipe => recipe.Id).FirstAsync(recipe => recipe.UserId == _factory.OtherUserId);
                latest.Description = "<img src=x onerror=alert(1)>";
                db.RecipeLikes.Add(new RecipeLike { RecipeId = latest.Id, UserId = _factory.OwnerId });
                db.Favorites.Add(new Favorite { RecipeId = latest.Id, UserId = _factory.OwnerId });
                await db.SaveChangesAsync();
            }

            using var client = _factory.CreateTestClient(_factory.OwnerId);
            using var response = await client.GetAsync("/Feed/Following");
            string raw = await response.Content.ReadAsStringAsync();
            Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", raw);
            Assert.DoesNotContain("<img src=x", raw);
            Assert.DoesNotContain("PasswordHash", raw);
            Assert.Contains("data-db-like-form", raw);
            Assert.Contains("data-db-save-form", raw);
            Assert.Contains("data-is-liked=\"true\"", raw);
            Assert.Contains("data-is-saved=\"true\"", raw);
            Assert.Contains("__RequestVerificationToken", raw);
            Assert.Contains("data-feed-comment-toggle", raw);
            string html = WebUtility.HtmlDecode(raw);
            string native = Regex.Match(FeedBatch(html), "data-next-page=\"([^\"]+)\"").Groups[1].Value;
            string nextPage = await HtmlAsync(client, native);
            Assert.Contains("<html", nextPage);
            Assert.NotEmpty(PostIds(nextPage));
        }

        [Fact]
        public async Task LazyComments_ShouldLimitRowsAndUseUniqueHeadingsAndCorrectPermissions()
        {
            int id = (await SeedFollowingAsync(1))[0];
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                for (int index = 0; index < 7; index++)
                {
                    db.RecipeComments.Add(new RecipeComment
                    {
                        RecipeId = id,
                        UserId = _factory.OtherUserId,
                        Content = $"Feed comment {index}"
                    });
                }

                await db.SaveChangesAsync();
            }

            using var client = _factory.CreateTestClient(_factory.OwnerId);
            string html = await HtmlAsync(client, $"/Feed/Comments/{id}");
            Assert.Equal(5, Regex.Matches(html, "data-db-comment data-comment-id").Count);
            Assert.Contains($"id=\"feedCommentsHeading_{id}\"", html);
            Assert.Contains("Xem tất cả bình luận", html);
            Assert.DoesNotContain("data-comment-delete", html);
            Assert.Contains("data-comment-create", html);
            using var author = _factory.CreateTestClient(_factory.OtherUserId);
            Assert.Contains("data-comment-delete", await HtmlAsync(author, $"/Feed/Comments/{id}"));
            using var missing = await client.GetAsync("/Feed/Comments/2147483647");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }

        [Fact]
        public async Task GuestInteractionLinks_FromPartialRequests_ShouldReturnToAFullPageAfterLogin()
        {
            int id = (await SeedFollowingAsync(1))[0];
            using var guest = _factory.CreateTestClient();
            string batch = await HtmlAsync(guest, "/Feed/More");
            Assert.Contains("/Account/Login", batch);
            Assert.DoesNotContain("returnUrl=%2FFeed%2FMore", batch);
            string comments = await HtmlAsync(guest, $"/Feed/Comments/{id}");
            Assert.Contains("/Account/Login", comments);
            Assert.DoesNotContain("returnUrl=%2FFeed%2FComments", comments);
        }

        private async Task<int[]> SeedFollowingAsync(int count)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Follows.Add(new Follow { FollowerId = _factory.OwnerId, FollowingId = _factory.OtherUserId });
            DateTime date = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
            for (int index = 0; index < count; index++)
            {
                db.Recipes.Add(new Recipe
                {
                    Name = $"Following recipe {index}",
                    UserId = _factory.OtherUserId,
                    Description = "From a followed kitchen",
                    Ingredients = "Rice",
                    Instructions = "Cook",
                    Category = "Món Việt",
                    CreatedAt = date
                });
            }

            db.Recipes.Add(new Recipe { Name = "Own private test recipe", UserId = _factory.OwnerId, CreatedAt = date });
            db.Recipes.Add(new Recipe { Name = "Unfollowed test recipe", CreatedAt = date });
            await db.SaveChangesAsync();
            return await db.Recipes.Where(recipe => recipe.UserId == _factory.OtherUserId)
                .OrderByDescending(recipe => recipe.CreatedAt).ThenByDescending(recipe => recipe.Id).Select(recipe => recipe.Id).ToArrayAsync();
        }

        private static async Task<string> HtmlAsync(HttpClient client, string url) => WebUtility.HtmlDecode(await client.GetStringAsync(url));

        private static string FeedBatch(string html) => Regex.Match(html, "<div class=\"feed-batch\"[\\s\\S]*?</div>\\s*(?=<div class=\"feed-loading|<div class=\"feed-empty|$)").Value is { Length: > 0 } batch ? batch : html;

        private static string? NextUrl(string html)
        {
            string value = Regex.Match(html, "data-next-url=\"([^\"]*)\"").Groups[1].Value;
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static int[] PostIds(string html) => Regex.Matches(html, "<article class=\"feed-post\"[^>]*data-recipe-id=\"(\\d+)\"")
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
    }
}
