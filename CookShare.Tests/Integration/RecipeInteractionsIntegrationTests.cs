using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Integration
{
    public sealed class RecipeInteractionsIntegrationTests : IAsyncLifetime
    {
        private readonly IsolatedRecipeWebApplicationFactory _factory = new();

        public Task InitializeAsync()
        {
            return _factory.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            await _factory.DisposeAsync();
        }

        [Fact]
        public async Task AjaxInteractions_ShouldPreservePrivateNotificationsAndCommentLinks()
        {
            int recipeId = await SeedRecipeAsync();
            using var actor = _factory.CreateTestClient(_factory.OtherUserId);
            using var owner = _factory.CreateTestClient(_factory.OwnerId);

            using var liked = await PostFormAsync(actor, $"/Recipe/{recipeId}/Likes");
            await AssertLikeResponseAsync(liked, recipeId, 1, true);
            using var repeated = await PostFormAsync(actor, $"/Recipe/{recipeId}/Likes");
            repeated.EnsureSuccessStatusCode();
            using var created = await PostFormAsync(actor, $"/Recipe/{recipeId}/Comments",
                new Dictionary<string, string> { ["Content"] = "Notification from the preserved comment UI" });
            created.EnsureSuccessStatusCode();
            int commentId = (await ReadJsonAsync(created)).GetProperty("comment").GetProperty("id").GetInt32();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var notifications = await db.Notifications.AsNoTracking().OrderBy(item => item.Type).ToListAsync();
                Assert.Equal(new[] { "Comment", "Like" }, notifications.Select(item => item.Type));
                Assert.All(notifications, item =>
                {
                    Assert.Equal(_factory.OwnerId, item.RecipientId);
                    Assert.Equal(_factory.OtherUserId, item.ActorId);
                    Assert.Equal(recipeId, item.RecipeId);
                });
                Assert.Equal(commentId, notifications[0].CommentId);
            }

            string details = await owner.GetStringAsync($"/Recipe/Details/{recipeId}");
            Assert.Contains($"id=\"comment-{commentId}\"", details);
            string ownerNotifications = WebUtility.HtmlDecode(await owner.GetStringAsync("/Notifications/Index"));
            Assert.Contains("đã thích công thức", ownerNotifications);
            Assert.Contains("đã bình luận", ownerNotifications);
            string actorNotifications = WebUtility.HtmlDecode(await actor.GetStringAsync("/Notifications/Index"));
            Assert.DoesNotContain("đã thích công thức", actorNotifications);
            Assert.DoesNotContain("đã bình luận", actorNotifications);
        }

        [Fact]
        public async Task Likes_ShouldPersistAcrossClientsAndKeepEachUsersStateIndependent()
        {
            var recipeId = await SeedRecipeAsync();
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var other = _factory.CreateTestClient(_factory.OtherUserId);
            using var anonymous = _factory.CreateTestClient();

            await AssertLikeSummaryAsync(anonymous, recipeId, 0, false);
            var spoof = new Dictionary<string, string>
            {
                ["UserId"] = _factory.OtherUserId,
                ["RecipeId"] = "2147483647"
            };
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var liked = await PostFormAsync(owner, $"/Recipe/{recipeId}/Likes", spoof);
                await AssertLikeResponseAsync(liked, recipeId, 1, true);
            }

            var firstLike = Assert.Single(await ReadLikesAsync());
            Assert.Equal(_factory.OwnerId, firstLike.UserId);
            Assert.Equal(recipeId, firstLike.RecipeId);
            Assert.True(firstLike.Id > 0);
            using var refreshedOwner = _factory.CreateTestClient(_factory.OwnerId);
            await AssertLikeSummaryAsync(refreshedOwner, recipeId, 1, true);
            await AssertLikeSummaryAsync(other, recipeId, 1, false);
            await AssertLikeSummaryAsync(anonymous, recipeId, 1, false);

            using var forgedUnlike = await PostFormAsync(other, $"/Recipe/{recipeId}/Likes/Unlike",
                new Dictionary<string, string> { ["UserId"] = _factory.OwnerId, ["RecipeId"] = "2147483647" });
            await AssertLikeResponseAsync(forgedUnlike, recipeId, 1, false);
            Assert.Equal(_factory.OwnerId, Assert.Single(await ReadLikesAsync()).UserId);

            using var otherLiked = await PostFormAsync(other, $"/Recipe/{recipeId}/Likes");
            await AssertLikeResponseAsync(otherLiked, recipeId, 2, true);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var unliked = await PostFormAsync(owner, $"/Recipe/{recipeId}/Likes/Unlike");
                await AssertLikeResponseAsync(unliked, recipeId, 1, false);
            }

            Assert.Equal(_factory.OtherUserId, Assert.Single(await ReadLikesAsync()).UserId);
            await AssertLikeSummaryAsync(refreshedOwner, recipeId, 1, false);
            await AssertLikeSummaryAsync(other, recipeId, 1, true);
        }

        [Fact]
        public async Task UnlikeWithoutExistingLike_ShouldReturnZeroWithoutCreatingRows()
        {
            var recipeId = await SeedRecipeAsync();
            using var owner = _factory.CreateTestClient(_factory.OwnerId);

            using var response = await PostFormAsync(owner, $"/Recipe/{recipeId}/Likes/Unlike");

            await AssertLikeResponseAsync(response, recipeId, 0, false);
            Assert.Empty(await ReadLikesAsync());
        }

        [Theory]
        [InlineData("Likes")]
        [InlineData("Likes/Unlike")]
        [InlineData("Comments")]
        [InlineData("Comments/1/Edit")]
        [InlineData("Comments/1/Delete")]
        public async Task AnonymousInteractionWrites_ShouldRequireLogin(string path)
        {
            var recipeId = await SeedRecipeAsync();
            using var client = _factory.CreateTestClient();

            using var response = await client.PostAsync($"/Recipe/{recipeId}/{path}",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["Content"] = "Anonymous comment" }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString ?? "");
            Assert.Empty(await ReadLikesAsync());
            Assert.Empty(await ReadCommentsAsync());
        }

        [Theory]
        [InlineData("Likes")]
        [InlineData("Likes/Unlike")]
        [InlineData("Comments")]
        [InlineData("Comments/1/Edit")]
        [InlineData("Comments/1/Delete")]
        public async Task InteractionWriteWithoutAntiforgery_ShouldNotChangeData(string path)
        {
            var recipeId = await SeedRecipeAsync();
            using var client = _factory.CreateTestClient(_factory.OwnerId);

            using var response = await client.PostAsync($"/Recipe/{recipeId}/{path}",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["Content"] = "Forged comment" }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(await ReadLikesAsync());
            Assert.Empty(await ReadCommentsAsync());
        }

        [Theory]
        [InlineData("Likes", "GET")]
        [InlineData("Likes", "POST")]
        [InlineData("Likes/Unlike", "POST")]
        [InlineData("Comments", "GET")]
        [InlineData("Comments", "POST")]
        [InlineData("Comments/1/Edit", "POST")]
        [InlineData("Comments/1/Delete", "POST")]
        public async Task InteractionForMissingRecipe_ShouldReturnNotFound(string path, string method)
        {
            using var client = _factory.CreateTestClient(_factory.OwnerId);

            using var response = method == "GET"
                ? await client.GetAsync($"/Recipe/2147483647/{path}")
                : await PostFormAsync(client, $"/Recipe/2147483647/{path}",
                    new Dictionary<string, string> { ["Content"] = "Missing recipe" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Comments_ShouldUseGeneratedIdentityAndAuthenticatedAuthorAndSurviveRefresh()
        {
            var recipeId = await SeedRecipeAsync();
            var oldestTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            await SeedCommentAsync(recipeId, _factory.OtherUserId, "Earlier public comment", oldestTime);
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            var before = DateTime.UtcNow.AddSeconds(-1);

            using var created = await PostFormAsync(owner, $"/Recipe/{recipeId}/Comments", new Dictionary<string, string>
            {
                ["Content"] = "  A genuinely useful cooking tip  ",
                ["UserId"] = _factory.OtherUserId,
                ["RecipeId"] = "2147483647",
                ["Id"] = "2147483647",
                ["CreatedAt"] = "2001-01-01",
                ["AuthorName"] = "Forged author"
            });

            created.EnsureSuccessStatusCode();
            var json = await ReadJsonAsync(created);
            Assert.Equal(2, json.GetProperty("count").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("message").GetString()));
            var comment = json.GetProperty("comment");
            var commentId = comment.GetProperty("id").GetInt32();
            Assert.InRange(commentId, 1, 2147483646);
            Assert.Equal(recipeId, comment.GetProperty("recipeId").GetInt32());
            Assert.Equal(_factory.OwnerId, comment.GetProperty("userId").GetString());
            Assert.Equal("Kitchen Owner", comment.GetProperty("authorName").GetString());
            Assert.Equal("A genuinely useful cooking tip", comment.GetProperty("content").GetString());
            Assert.True(comment.GetProperty("canManage").GetBoolean());

            using var refreshedAnonymous = _factory.CreateTestClient();
            using var publicResponse = await refreshedAnonymous.GetAsync($"/Recipe/{recipeId}/Comments");
            publicResponse.EnsureSuccessStatusCode();
            var publicJson = await ReadJsonAsync(publicResponse);
            Assert.Equal(recipeId, publicJson.GetProperty("recipeId").GetInt32());
            Assert.Equal(2, publicJson.GetProperty("count").GetInt32());
            var comments = publicJson.GetProperty("comments").EnumerateArray().ToArray();
            Assert.Equal(2, comments.Length);
            Assert.All(comments, item => Assert.False(item.GetProperty("canManage").GetBoolean()));
            var persisted = (await ReadCommentsAsync()).Single(item => item.Id == commentId);
            Assert.Equal("A genuinely useful cooking tip", persisted.Content);
            Assert.Equal(_factory.OwnerId, persisted.UserId);
            Assert.InRange(persisted.CreatedAt, before, DateTime.UtcNow.AddSeconds(1));

            using var ownerResponse = await owner.GetAsync($"/Recipe/{recipeId}/Comments");
            var ownerJson = await ReadJsonAsync(ownerResponse);
            var ownerComments = ownerJson.GetProperty("comments").EnumerateArray().ToArray();
            Assert.True(ownerComments.Single(item => item.GetProperty("id").GetInt32() == commentId)
                .GetProperty("canManage").GetBoolean());
            Assert.False(ownerComments.Single(item => item.GetProperty("id").GetInt32() != commentId)
                .GetProperty("canManage").GetBoolean());
        }

        [Fact]
        public async Task EmptyComments_ShouldBePublicAndReturnZeroCount()
        {
            var recipeId = await SeedRecipeAsync();
            using var client = _factory.CreateTestClient();
            using var response = await client.GetAsync($"/Recipe/{recipeId}/Comments");

            response.EnsureSuccessStatusCode();
            var json = await ReadJsonAsync(response);
            Assert.Equal(recipeId, json.GetProperty("recipeId").GetInt32());
            Assert.Equal(0, json.GetProperty("count").GetInt32());
            Assert.Empty(json.GetProperty("comments").EnumerateArray());
        }

        [Theory]
        [InlineData("")]
        [InlineData("   \r\n\t ")]
        [InlineData(null)]
        public async Task EmptyComment_ShouldBeRejectedWithoutPersisting(string? content)
        {
            var recipeId = await SeedRecipeAsync();
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            var form = new Dictionary<string, string>();
            if (content != null)
            {
                form["Content"] = content;
            }

            using var response = await PostFormAsync(owner, $"/Recipe/{recipeId}/Comments", form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(await ReadCommentsAsync());
        }

        [Theory]
        [InlineData(500, HttpStatusCode.OK)]
        [InlineData(501, HttpStatusCode.BadRequest)]
        public async Task CommentLength_ShouldValidateTrimmedContent(int length, HttpStatusCode expected)
        {
            var recipeId = await SeedRecipeAsync();
            using var owner = _factory.CreateTestClient(_factory.OwnerId);

            using var response = await PostFormAsync(owner, $"/Recipe/{recipeId}/Comments",
                new Dictionary<string, string> { ["Content"] = "  " + new string('x', length) + "  " });

            Assert.Equal(expected, response.StatusCode);
            if (expected == HttpStatusCode.OK)
            {
                Assert.Equal(new string('x', length), Assert.Single(await ReadCommentsAsync()).Content);
            }
            else
            {
                Assert.Empty(await ReadCommentsAsync());
            }
        }

        [Fact]
        public async Task OwnCommentEditAndDelete_ShouldPreserveOwnershipCreationTimeAndKeepCountsAccurate()
        {
            var recipeId = await SeedRecipeAsync();
            var timestamp = new DateTime(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            var commentId = await SeedCommentAsync(recipeId, _factory.OwnerId, "Original comment", timestamp);
            await SeedCommentAsync(recipeId, _factory.OtherUserId, "Keep this comment", timestamp);
            using var owner = _factory.CreateTestClient(_factory.OwnerId);

            using var edited = await PostFormAsync(owner, $"/Recipe/{recipeId}/Comments/{commentId}/Edit",
                new Dictionary<string, string>
                {
                    ["Content"] = "  Updated cooking tip  ",
                    ["UserId"] = _factory.OtherUserId,
                    ["CreatedAt"] = "2000-01-01"
                });

            edited.EnsureSuccessStatusCode();
            var editJson = await ReadJsonAsync(edited);
            Assert.Equal(2, editJson.GetProperty("count").GetInt32());
            Assert.Equal("Updated cooking tip", editJson.GetProperty("comment").GetProperty("content").GetString());
            var persisted = (await ReadCommentsAsync()).Single(item => item.Id == commentId);
            Assert.Equal(timestamp, persisted.CreatedAt);
            Assert.Equal(_factory.OwnerId, persisted.UserId);
            Assert.Equal(recipeId, persisted.RecipeId);

            using var deleted = await PostFormAsync(owner, $"/Recipe/{recipeId}/Comments/{commentId}/Delete");

            deleted.EnsureSuccessStatusCode();
            var deleteJson = await ReadJsonAsync(deleted);
            Assert.Equal(commentId, deleteJson.GetProperty("commentId").GetInt32());
            Assert.Equal(1, deleteJson.GetProperty("count").GetInt32());
            Assert.Equal("Keep this comment", Assert.Single(await ReadCommentsAsync()).Content);
        }

        [Theory]
        [InlineData("Edit")]
        [InlineData("Delete")]
        public async Task OtherUsersCommentMutation_ShouldBeForbiddenEvenWithForgedUserId(string action)
        {
            var recipeId = await SeedRecipeAsync();
            var commentId = await SeedCommentAsync(recipeId, _factory.OwnerId, "Protected comment");
            using var other = _factory.CreateTestClient(_factory.OtherUserId);

            using var response = await PostFormAsync(other, $"/Recipe/{recipeId}/Comments/{commentId}/{action}",
                new Dictionary<string, string> { ["Content"] = "Tampered", ["UserId"] = _factory.OwnerId });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("Protected comment", Assert.Single(await ReadCommentsAsync()).Content);
        }

        [Fact]
        public async Task PostedIdentifiers_ShouldNotRetargetCommentEditOrDeleteToAnotherOwnedComment()
        {
            var recipeId = await SeedRecipeAsync();
            var otherRecipeId = await SeedRecipeAsync();
            var commentId = await SeedCommentAsync(recipeId, _factory.OwnerId, "Route-selected comment");
            var decoyCommentId = await SeedCommentAsync(otherRecipeId, _factory.OwnerId, "Keep the other recipe comment");
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            var form = new Dictionary<string, string>
            {
                ["Content"] = "Edit the route-selected comment",
                ["RecipeId"] = otherRecipeId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["CommentId"] = decoyCommentId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            using var edited = await PostFormAsync(owner, $"/Recipe/{recipeId}/Comments/{commentId}/Edit", form);

            edited.EnsureSuccessStatusCode();
            var comments = await ReadCommentsAsync();
            Assert.Equal("Edit the route-selected comment", comments.Single(item => item.Id == commentId).Content);
            Assert.Equal("Keep the other recipe comment", comments.Single(item => item.Id == decoyCommentId).Content);
            using var deleted = await PostFormAsync(owner, $"/Recipe/{recipeId}/Comments/{commentId}/Delete", form);

            deleted.EnsureSuccessStatusCode();
            var remaining = Assert.Single(await ReadCommentsAsync());
            Assert.Equal(decoyCommentId, remaining.Id);
            Assert.Equal(otherRecipeId, remaining.RecipeId);
            Assert.Equal("Keep the other recipe comment", remaining.Content);
        }

        [Theory]
        [InlineData("Edit", true)]
        [InlineData("Delete", true)]
        [InlineData("Edit", false)]
        [InlineData("Delete", false)]
        public async Task MissingOrDifferentRecipesComment_ShouldReturnNotFound(string action, bool existingComment)
        {
            var firstRecipe = await SeedRecipeAsync();
            var secondRecipe = await SeedRecipeAsync();
            var commentId = await SeedCommentAsync(firstRecipe, _factory.OwnerId, "Original recipe only");
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            var requestedId = existingComment ? commentId : 2147483647;

            using var response = await PostFormAsync(owner, $"/Recipe/{secondRecipe}/Comments/{requestedId}/{action}",
                new Dictionary<string, string> { ["Content"] = "Wrong recipe" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("Original recipe only", Assert.Single(await ReadCommentsAsync()).Content);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("overlength")]
        public async Task InvalidCommentEdit_ShouldPreserveOriginalComment(string value)
        {
            var recipeId = await SeedRecipeAsync();
            var commentId = await SeedCommentAsync(recipeId, _factory.OwnerId, "Original comment");
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            var content = value == "overlength" ? new string('x', 501) : value;

            using var response = await PostFormAsync(owner, $"/Recipe/{recipeId}/Comments/{commentId}/Edit",
                new Dictionary<string, string> { ["Content"] = content });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Original comment", Assert.Single(await ReadCommentsAsync()).Content);
        }

        [Fact]
        public async Task CommentMarkup_ShouldEncodeContentAndExposeManagementOnlyToItsAuthor()
        {
            const string content = "<img src=x onerror=alert(1)>";
            var recipeId = await SeedRecipeAsync();
            var commentId = await SeedCommentAsync(recipeId, _factory.OwnerId, content);
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var other = _factory.CreateTestClient(_factory.OtherUserId);
            using var anonymous = _factory.CreateTestClient();

            foreach (var client in new[] { owner, other, anonymous })
            {
                var html = await client.GetStringAsync($"/Recipe/Details/{recipeId}");
                Assert.DoesNotContain(content, html);
                Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
                Assert.Contains($"data-comment-id=\"{commentId}\"", html);
                Assert.Contains("Kitchen Owner", html);
                Assert.Single(Regex.Matches(html, @"<section\b[^>]*\bdata-db-recipe-comments\b", RegexOptions.IgnoreCase));
                Assert.Matches("data-db-comment-count[^>]*>\\s*1\\s*</span>", html);
                if (client == owner)
                {
                    Assert.Contains("data-comment-edit", html);
                    Assert.Contains("data-comment-delete", html);
                }
                else
                {
                    Assert.DoesNotContain("data-comment-edit", html);
                    Assert.DoesNotContain("data-comment-delete", html);
                }
            }
        }

        [Fact]
        public async Task PublishedPages_ShouldRenderPersistedLikeCountAndCurrentUsersStateWithoutLegacyScripts()
        {
            var recipeId = await SeedRecipeAsync();
            using var owner = _factory.CreateTestClient(_factory.OwnerId);
            using var other = _factory.CreateTestClient(_factory.OtherUserId);
            using var anonymous = _factory.CreateTestClient();
            using var liked = await PostFormAsync(owner, $"/Recipe/{recipeId}/Likes");
            liked.EnsureSuccessStatusCode();

            foreach (var url in new[] { "/", "/Recipe", $"/Recipe/Details/{recipeId}", "/Profile?userId=" + _factory.OwnerId })
            {
                foreach (var client in new[] { owner, other, anonymous })
                {
                    var html = await client.GetStringAsync(url);
                    var wrappers = Regex.Matches(html, @"<[^>]+data-db-recipe-like\b[^>]*>", RegexOptions.IgnoreCase)
                        .Select(match => match.Value)
                        .Where(tag => tag.Contains($"data-recipe-id=\"{recipeId}\"", StringComparison.Ordinal))
                        .ToArray();
                    Assert.NotEmpty(wrappers);
                    var expectedLiked = client == owner ? "true" : "false";
                    Assert.All(wrappers, tag => Assert.Contains($"data-is-liked=\"{expectedLiked}\"", tag));
                    Assert.Matches("class=\"[^\"]*recipe-like-count-display[^\"]*\"[^>]*>\\s*1\\s*</span>", html);
                    if (client != anonymous)
                    {
                        Assert.Matches($"<button[^>]*class=\"[^\"]*recipe-like-btn[^\"]*\"[^>]*aria-pressed=\"{expectedLiked}\"", html);
                    }

                    Assert.DoesNotContain("src=\"/js/recipe-likes.js", html);
                    Assert.DoesNotContain("src=\"/js/recipe-comments.js", html);
                    Assert.DoesNotContain("localStorage", html);
                }
            }
        }

        [Fact]
        public async Task RealCookieRegistrationLoginLogout_ShouldKeepDatabaseLikesAndRestorePersonalState()
        {
            await using var cookieFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultForbidScheme = IdentityConstants.ApplicationScheme;
                })));
            int recipeId;
            using (var scope = cookieFactory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Database.EnsureCreatedAsync();
                var recipe = new Recipe
                {
                    Name = "Cookie authentication recipe",
                    Description = "Recipe for real cookie authentication",
                    Ingredients = "Fresh ingredients",
                    Instructions = "Cook together",
                    Category = "Dinner"
                };
                db.Recipes.Add(recipe);
                await db.SaveChangesAsync();
                recipeId = recipe.Id;
            }

            using var client = cookieFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
                BaseAddress = new Uri("https://localhost")
            });
            const string userName = "cookie-cook";
            const string email = "cookie-cook@example.com";
            const string password = "TestPassword123!";
            using var registered = await PostFormAsync(client, "/Account/Register", new Dictionary<string, string>
            {
                ["LastName"] = "Cookie",
                ["FirstName"] = "Cook",
                ["UserName"] = userName,
                ["Email"] = email,
                ["Password"] = password,
                ["ConfirmPassword"] = password
            }, "/Account/Register");

            Assert.Equal(HttpStatusCode.Redirect, registered.StatusCode);
            Assert.Contains(registered.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("CookShare.Auth=", StringComparison.Ordinal));
            using var liked = await PostFormAsync(client, $"/Recipe/{recipeId}/Likes");
            await AssertLikeResponseAsync(liked, recipeId, 1, true);
            var likedBy = Assert.Single(await ReadLikesAsync(cookieFactory.Services)).UserId;
            Assert.NotEqual(_factory.OwnerId, likedBy);

            using var forgedLogout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.BadRequest, forgedLogout.StatusCode);
            await AssertLikeSummaryAsync(client, recipeId, 1, true);
            using var logout = await PostFormAsync(client, "/Account/Logout");
            Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
            await AssertLikeSummaryAsync(client, recipeId, 1, false);
            using var anonymousWrite = await client.PostAsync($"/Recipe/{recipeId}/Likes", new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.Redirect, anonymousWrite.StatusCode);
            Assert.Contains("/Account/Login", anonymousWrite.Headers.Location?.OriginalString ?? "");

            using var login = await PostFormAsync(client, "/Account/Login?returnUrl=%2FRecipe", new Dictionary<string, string>
            {
                ["UserNameOrEmail"] = email,
                ["Password"] = password
            }, "/Account/Login");

            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            Assert.Equal("/Recipe", login.Headers.Location?.OriginalString);
            await AssertLikeSummaryAsync(client, recipeId, 1, true);
            Assert.Equal(likedBy, Assert.Single(await ReadLikesAsync(cookieFactory.Services)).UserId);
        }

        private async Task<int> SeedRecipeAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var recipe = new Recipe
            {
                Name = "Persistent interaction recipe",
                Description = "Database-backed recipe",
                Ingredients = "Fresh ingredients",
                Instructions = "Mix and cook",
                Category = "Dinner",
                UserId = _factory.OwnerId
            };
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            return recipe.Id;
        }

        private async Task<int> SeedCommentAsync(int recipeId, string userId, string content, DateTime? createdAt = null)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var comment = new RecipeComment
            {
                RecipeId = recipeId,
                UserId = userId,
                Content = content,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };
            db.RecipeComments.Add(comment);
            await db.SaveChangesAsync();
            return comment.Id;
        }

        private async Task<List<RecipeLike>> ReadLikesAsync(IServiceProvider? services = null)
        {
            using var scope = (services ?? _factory.Services).CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.RecipeLikes.AsNoTracking().ToListAsync();
        }

        private async Task<List<RecipeComment>> ReadCommentsAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.RecipeComments.AsNoTracking().ToListAsync();
        }

        private static async Task AssertLikeSummaryAsync(HttpClient client, int recipeId, int count, bool liked)
        {
            using var response = await client.GetAsync($"/Recipe/{recipeId}/Likes");
            await AssertLikeResponseAsync(response, recipeId, count, liked, requireMessage: false);
        }

        private static async Task AssertLikeResponseAsync(HttpResponseMessage response, int recipeId, int count, bool liked, bool requireMessage = true)
        {
            response.EnsureSuccessStatusCode();
            var json = await ReadJsonAsync(response);
            Assert.Equal(recipeId, json.GetProperty("recipeId").GetInt32());
            Assert.Equal(count, json.GetProperty("likeCount").GetInt32());
            Assert.Equal(liked, json.GetProperty("isLiked").GetBoolean());
            if (requireMessage)
            {
                Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("message").GetString()));
            }
        }

        private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        {
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        private static async Task<HttpResponseMessage> PostFormAsync(
            HttpClient client,
            string url,
            Dictionary<string, string>? form = null,
            string tokenPage = "/Recipe/Create")
        {
            using var page = await client.GetAsync(tokenPage);
            page.EnsureSuccessStatusCode();
            var html = await page.Content.ReadAsStringAsync();
            var input = Regex.Matches(html, @"<input\b[^>]*>", RegexOptions.IgnoreCase)
                .Select(match => match.Value)
                .First(tag => tag.Contains("name=\"__RequestVerificationToken\"", StringComparison.Ordinal));
            var match = Regex.Match(input, "value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
            Assert.True(match.Success);
            var values = form == null ? new Dictionary<string, string>() : new Dictionary<string, string>(form);
            values["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value);
            return await client.PostAsync(url, new FormUrlEncodedContent(values));
        }
    }
}
