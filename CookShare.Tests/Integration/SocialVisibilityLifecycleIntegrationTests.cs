using System.Net;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.Services;
using CookShare.Tests.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CookShare.Tests.Integration
{
    public sealed class SocialVisibilityLifecycleIntegrationTests : IAsyncLifetime
    {
        private const string HiddenName = "Private visibility regression recipe 74839";
        private const string KeptName = "Remaining community recipe 51207";
        private readonly LifecycleFactory _factory = new();
        private int _hiddenRecipeId;
        private int _keptRecipeId;
        private int _hiddenPostId;
        private int _keptPostId;
        private int _ownPostId;
        private int _keptCollectionId;
        private string _hiddenImage = string.Empty;
        private string _hiddenSocialImage = string.Empty;

        public async Task InitializeAsync()
        {
            await _factory.InitializeAsync();
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (string id in new[] { "departing", "remaining" })
            {
                var created = await users.CreateAsync(new ApplicationUser { Id = id, UserName = id, Email = id + "@example.test", DisplayName = id }, "LifecycleTests123!");
                Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
            }
            _hiddenImage = await _factory.RecipeImages.SaveAsync(SocialTestDatabase.Image());
            string remainingImage = await _factory.RecipeImages.SaveAsync(SocialTestDatabase.Image());
            var hiddenRecipe = NewRecipe(HiddenName, "departing", _hiddenImage);
            var keptRecipe = NewRecipe(KeptName, "remaining", remainingImage);
            db.Recipes.AddRange(hiddenRecipe, keptRecipe);
            await db.SaveChangesAsync();
            _hiddenRecipeId = hiddenRecipe.Id;
            _keptRecipeId = keptRecipe.Id;
            var posts = scope.ServiceProvider.GetRequiredService<CookedPostService>();
            _hiddenPostId = await posts.CreateAsync("remaining", hiddenRecipe.Id, "hidden parent post", Guid.NewGuid(), SocialTestDatabase.Image());
            _ownPostId = await posts.CreateAsync("departing", keptRecipe.Id, "departing post", Guid.NewGuid(), SocialTestDatabase.Image());
            _keptPostId = await posts.CreateAsync("remaining", keptRecipe.Id, "remaining post", Guid.NewGuid(), SocialTestDatabase.Image());
            _hiddenSocialImage = await db.Set<CookedRecipePost>().Where(post => post.Id == _hiddenPostId).Select(post => post.ImageFileName).SingleAsync();
            var ownCollection = new RecipeCollection { OwnerId = "departing", Name = "Departing board", IsPublic = true };
            var keptCollection = new RecipeCollection { OwnerId = "remaining", Name = "Remaining board", IsPublic = true };
            db.RecipeCollections.AddRange(ownCollection, keptCollection);
            await db.SaveChangesAsync();
            _keptCollectionId = keptCollection.Id;
            db.CollectionRecipes.AddRange(new CollectionRecipe { CollectionId = ownCollection.Id, RecipeId = keptRecipe.Id },
                new CollectionRecipe { CollectionId = keptCollection.Id, RecipeId = keptRecipe.Id },
                new CollectionRecipe { CollectionId = keptCollection.Id, RecipeId = hiddenRecipe.Id });
            db.ProfileFeaturedRecipes.AddRange(new ProfileFeaturedRecipe { UserId = "departing", RecipeId = hiddenRecipe.Id, DisplayOrder = 1 },
                new ProfileFeaturedRecipe { UserId = "remaining", RecipeId = keptRecipe.Id, DisplayOrder = 1 });
            foreach (var (user, recipe) in new[] { ("departing", keptRecipe.Id), ("remaining", hiddenRecipe.Id) })
            {
                db.RecipeLikes.Add(new RecipeLike { UserId = user, RecipeId = recipe });
                db.RecipeComments.Add(new RecipeComment { UserId = user, RecipeId = recipe, Content = "A comment" });
                db.Favorites.Add(new Favorite { UserId = user, RecipeId = recipe });
                db.RecipeRatings.Add(new RecipeRating { UserId = user, RecipeId = recipe, Stars = 4 });
            }
            db.Follows.AddRange(new Follow { FollowerId = "departing", FollowingId = "remaining" },
                new Follow { FollowerId = "remaining", FollowingId = "departing" });
            var report = new ContentReport { ReporterId = "departing", TargetType = ContentTargetType.CookedPost, TargetId = _ownPostId, Reason = ReportReason.Other };
            db.ContentReports.Add(report);
            await db.SaveChangesAsync();
            db.ModerationActions.Add(new ModerationAction
            {
                ReportId = report.Id,
                ModeratorId = "departing",
                TargetType = report.TargetType,
                TargetId = report.TargetId,
                Action = ModerationActionType.Hide,
                Reason = "Lifecycle audit retained"
            });
            hiddenRecipe.IsHidden = true;
            (await db.CookedRecipePosts.SingleAsync(post => post.Id == _ownPostId)).IsHidden = true;
            await db.SaveChangesAsync();
        }

        public async Task DisposeAsync() => await _factory.DisposeAsync();

        [Fact]
        public async Task HiddenRecipe_IsAbsentFromEveryPublicSurface_AndBothMediaRoutesAndInteractionAreBlocked()
        {
            using var client = _factory.CreateTestClient("remaining");
            foreach (string path in new[] { "/", "/?feed=following", "/Recipe", "/Profile?userId=departing", "/Profile?userId=remaining", "/Collections/" + _keptCollectionId })
            {
                using var response = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
                Assert.DoesNotContain(HiddenName, html);
                Assert.DoesNotContain("hidden parent post", html);
                Assert.DoesNotContain($"/media/cooked-posts/{_hiddenPostId}/", html);
            }
            foreach (string path in new[] { "/Recipe/Details/" + _hiddenRecipeId, "/CookedPosts/Details/" + _hiddenPostId,
                _hiddenImage, $"/media/cooked-posts/{_hiddenPostId}/{_hiddenSocialImage}" })
            {
                using var response = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
            using var form = await client.GetAsync("/Recipe/Details/" + _keptRecipeId);
            string token = ExtractToken(await form.Content.ReadAsStringAsync());
            foreach (string endpoint in new[] { "/Recipe/Like/" + _hiddenRecipeId, $"/Recipe/{_hiddenRecipeId}/Favorites", $"/Recipe/{_hiddenRecipeId}/Ratings" })
            {
                using var response = await client.PostAsync(endpoint, new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["Stars"] = "5"
                }));
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
        }

        [Fact]
        public async Task DeleteAccount_RemovesHiddenOwnedContentAndMedia_PreservesOtherBoardsAndAudit()
        {
            _factory.SocialImages.BeforeDelete = async file =>
            {
                using var check = _factory.Services.CreateScope();
                var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.False(await db.Users.AnyAsync(user => user.Id == "departing"));
                Assert.False(await db.CookedRecipePosts.IgnoreQueryFilters().AnyAsync(post => post.ImageFileName == file));
            };
            using (var scope = _factory.Services.CreateScope())
            {
                var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync("departing");
                var result = await scope.ServiceProvider.GetRequiredService<AccountDeletionService>().DeleteAsync(user!);
                Assert.True(result.Succeeded);
            }
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.False(await db.Users.AnyAsync(user => user.Id == "departing"));
                Assert.Equal(_keptRecipeId, (await db.Recipes.IgnoreQueryFilters().SingleAsync()).Id);
                Assert.Equal(_keptPostId, (await db.CookedRecipePosts.IgnoreQueryFilters().SingleAsync()).Id);
                Assert.Equal(_keptCollectionId, (await db.RecipeCollections.SingleAsync()).Id);
                var membership = await db.CollectionRecipes.IgnoreQueryFilters().SingleAsync();
                Assert.Equal(_keptRecipeId, membership.RecipeId);
                Assert.Equal(_keptCollectionId, membership.CollectionId);
                Assert.Empty(await db.RecipeLikes.IgnoreQueryFilters().ToListAsync());
                Assert.Empty(await db.RecipeComments.IgnoreQueryFilters().ToListAsync());
                Assert.Empty(await db.Favorites.IgnoreQueryFilters().ToListAsync());
                Assert.Empty(await db.RecipeRatings.IgnoreQueryFilters().ToListAsync());
                Assert.Empty(await db.Follows.ToListAsync());
                Assert.Null((await db.ContentReports.SingleAsync()).ReporterId);
                var audit = await db.ModerationActions.SingleAsync();
                Assert.Null(audit.ModeratorId);
                Assert.Equal(_ownPostId, audit.TargetId);
                Assert.Equal("Lifecycle audit retained", audit.Reason);
                Assert.DoesNotContain(_hiddenImage, _factory.RecipeImages.Files);
                string keptFile = (await db.CookedRecipePosts.IgnoreQueryFilters().SingleAsync()).ImageFileName;
                Assert.Equal(keptFile, Assert.Single(_factory.SocialImages.Files.Keys));
                Assert.Single(_factory.RecipeImages.Files);
            }
        }

        private static Recipe NewRecipe(string name, string owner, string image) => new()
        {
            Name = name,
            UserId = owner,
            Image = image,
            Category = "Món Việt",
            Description = "A recipe",
            Ingredients = "Vegetables",
            Instructions = "Cook thoroughly"
        };

        private static string ExtractToken(string html)
        {
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant);
            Assert.True(match.Success);
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        private sealed class LifecycleFactory() : AccountSettingsWebApplicationFactory(false)
        {
            public RecordingSocialImageStorage SocialImages { get; } = new();
            public RecordingRecipeImageStorage RecipeImages { get; } = new();
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ISocialImageStorage>();
                    services.RemoveAll<IRecipeImageStorage>();
                    services.AddSingleton<ISocialImageStorage>(SocialImages);
                    services.AddSingleton<IRecipeImageStorage>(RecipeImages);
                });
            }
        }

        private sealed class RecordingRecipeImageStorage : IRecipeImageStorage
        {
            public HashSet<string> Files { get; } = new(StringComparer.Ordinal);
            public Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
            {
                string reference = "/uploads/recipes/" + Guid.NewGuid().ToString("N") + ".png";
                Files.Add(reference);
                return Task.FromResult(reference);
            }
            public Task DeleteAsync(string imageReference, CancellationToken cancellationToken = default)
            {
                Files.Remove(imageReference);
                return Task.CompletedTask;
            }
            public RecipeImageFile? OpenRead(string fileName) => Files.Contains("/uploads/recipes/" + fileName)
                ? new RecipeImageFile(new MemoryStream(new byte[] { 1, 2, 3 }), "image/png") : null;
        }
    }
}
