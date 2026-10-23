using System.Reflection;
using System.Security.Claims;
using CookShare.Controllers;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Controllers
{
    public class ProfileControllerTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Index_AnonymousWithoutTarget_ShouldChallenge(string? userId)
        {
            using var services = CreateServices();
            var controller = CreateController(services);

            Assert.IsType<ChallengeResult>(await controller.Index(userId));
        }

        [Theory]
        [InlineData("demo-nguyen-van-an")]
        [InlineData("demo-tran-minh-anh")]
        [InlineData("demo-le-hoang-nam")]
        public async Task Index_WithUserId_ShouldReturnRequestedProfile(string userId)
        {
            using var services = CreateServices(NewUser(userId));
            var controller = CreateController(services);

            var result = Assert.IsType<ViewResult>(await controller.Index(userId));
            var model = Assert.IsType<ProfileViewModel>(result.Model);

            Assert.Equal(userId, model.UserId);
            Assert.False(model.IsOwner);
            Assert.False(model.IsFollowing);
            Assert.Empty(model.Recipes);
            Assert.Empty(model.Followers);
            Assert.Empty(model.Following);
            Assert.Empty(model.Favorites);
        }

        [Fact]
        public async Task Index_MissingUser_ShouldReturnNotFound()
        {
            using var services = CreateServices();
            var controller = CreateController(services);

            Assert.IsType<NotFoundResult>(await controller.Index("missing-user"));
        }

        [Theory]
        [InlineData("owner", true, false)]
        [InlineData("viewer", false, true)]
        [InlineData(null, false, false)]
        public async Task Index_ShouldLoadProfileDataAndKeepFavoritesPrivate(
            string? currentUserId, bool isOwner, bool isFollowing)
        {
            var owner = NewUser("owner");
            using var services = CreateServices(owner, NewUser("viewer"), NewUser("third"));
            var db = services.GetRequiredService<ApplicationDbContext>();
            var older = NewRecipe(1, "owner");
            var newer = NewRecipe(2, "owner");
            newer.CreatedAt = older.CreatedAt.AddDays(1);
            var otherRecipe = NewRecipe(3, "third");
            db.Recipes.AddRange(older, newer, otherRecipe);
            db.Follows.AddRange(
                new Follow { FollowerId = "viewer", FollowingId = "owner" },
                new Follow { FollowerId = "owner", FollowingId = "third" });
            db.Favorites.Add(new Favorite { UserId = "owner", RecipeId = otherRecipe.Id });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var controller = CreateController(services, currentUserId);

            // Without a target, authenticated owners should see their own profile.
            var result = Assert.IsType<ViewResult>(await controller.Index(isOwner ? null : "owner"));
            var model = Assert.IsType<ProfileViewModel>(result.Model);

            Assert.Equal(owner.Id, model.UserId);
            Assert.Equal(owner.UserName, model.UserName);
            Assert.Equal(owner.DisplayName, model.DisplayName);
            Assert.Equal(owner.AvatarUrl, model.AvatarUrl);
            Assert.Equal(owner.CreatedAt, model.JoinedAt);
            Assert.Equal(isOwner, model.IsOwner);
            Assert.Equal(isFollowing, model.IsFollowing);
            Assert.Equal(2, model.RecipeCount);
            Assert.Equal(new[] { newer.Id, older.Id }, model.Recipes.Select(recipe => recipe.Id));
            Assert.Equal(1, model.FollowersCount);
            Assert.Equal("viewer", Assert.Single(model.Followers).UserId);
            Assert.Equal(1, model.FollowingCount);
            Assert.Equal("third", Assert.Single(model.Following).UserId);
            if (isOwner)
            {
                Assert.Equal(otherRecipe.Id, Assert.Single(model.Favorites).Id);
            }
            else
            {
                Assert.Empty(model.Favorites);
            }
        }

        [Fact]
        public async Task Edit_ShouldReturnCurrentUsersForm()
        {
            var user = NewUser("owner");
            using var services = CreateServices(user);
            var controller = CreateController(services, user.Id);

            var result = Assert.IsType<ViewResult>(await controller.Edit());
            var model = Assert.IsType<EditProfileViewModel>(result.Model);

            Assert.Equal(user.DisplayName, model.DisplayName);
            Assert.Equal(user.AvatarUrl, model.AvatarUrl);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("missing-user")]
        public async Task Edit_WithoutExistingUser_ShouldChallenge(string? userId)
        {
            using var services = CreateServices();
            var controller = CreateController(services, userId);

            Assert.IsType<ChallengeResult>(await controller.Edit());
            Assert.IsType<ChallengeResult>(await controller.Edit(new EditProfileViewModel { DisplayName = "New name" }));
        }

        [Theory]
        [InlineData(" /img/avatar.png ", "/img/avatar.png")]
        [InlineData(" https://example.com/avatar.png ", "https://example.com/avatar.png")]
        [InlineData(null, null)]
        [InlineData("   ", null)]
        public async Task Edit_ValidForm_ShouldUpdateOnlyCurrentUser(string? avatar, string? expectedAvatar)
        {
            using var services = CreateServices(NewUser("owner"), NewUser("other"));
            var controller = CreateController(services, "owner");
            var model = new EditProfileViewModel { DisplayName = "  New name  ", AvatarUrl = avatar };

            var result = Assert.IsType<RedirectToActionResult>(await controller.Edit(model));
            var db = services.GetRequiredService<ApplicationDbContext>();
            db.ChangeTracker.Clear();
            var updated = await db.Users.SingleAsync(user => user.Id == "owner");
            var other = await db.Users.SingleAsync(user => user.Id == "other");

            Assert.Equal("New name", updated.DisplayName);
            Assert.Equal(expectedAvatar, updated.AvatarUrl);
            Assert.Equal("Display other", other.DisplayName);
            Assert.Equal(nameof(ProfileController.Index), result.ActionName);
            Assert.NotNull(controller.TempData["Success"]);
        }

        [Theory]
        [InlineData("http://example.com/avatar.png")]
        [InlineData("//example.com/avatar.png")]
        [InlineData("/\\example.com/avatar.png")]
        [InlineData("https://user:password@example.com/avatar.png")]
        public async Task Edit_InvalidAvatar_ShouldRedisplayFormWithoutSaving(string avatar)
        {
            using var services = CreateServices(NewUser("owner"));
            var controller = CreateController(services, "owner");
            var model = new EditProfileViewModel { DisplayName = "New name", AvatarUrl = avatar };

            var result = Assert.IsType<ViewResult>(await controller.Edit(model));

            Assert.Same(model, result.Model);
            Assert.Contains(nameof(model.AvatarUrl), controller.ModelState.Keys);
            var db = services.GetRequiredService<ApplicationDbContext>();
            db.ChangeTracker.Clear();
            Assert.Equal("Display owner", (await db.Users.SingleAsync()).DisplayName);
        }

        [Fact]
        public async Task Edit_InvalidModelState_ShouldRedisplayFormWithoutSaving()
        {
            using var services = CreateServices(NewUser("owner"));
            var controller = CreateController(services, "owner");
            var model = new EditProfileViewModel { DisplayName = "" };
            controller.ModelState.AddModelError(nameof(model.DisplayName), "Required");

            var result = Assert.IsType<ViewResult>(await controller.Edit(model));

            Assert.Same(model, result.Model);
            var db = services.GetRequiredService<ApplicationDbContext>();
            db.ChangeTracker.Clear();
            Assert.Equal("Display owner", (await db.Users.SingleAsync()).DisplayName);
        }

        [Fact]
        public void EditActions_ShouldRequireAuthorizationAndCorrectHttpVerbs()
        {
            var controllerType = typeof(ProfileController);
            var get = controllerType.GetMethod(nameof(ProfileController.Edit), Type.EmptyTypes)!;
            var post = controllerType.GetMethod(nameof(ProfileController.Edit), new[] { typeof(EditProfileViewModel) })!;

            Assert.NotNull(get.GetCustomAttribute<AuthorizeAttribute>());
            Assert.NotNull(get.GetCustomAttribute<HttpGetAttribute>());
            Assert.NotNull(post.GetCustomAttribute<AuthorizeAttribute>());
            Assert.NotNull(post.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(post.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            Assert.Null(get.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(post.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        private static ServiceProvider CreateServices(params ApplicationUser[] users)
        {
            var services = new ServiceCollection();
            var databaseName = Guid.NewGuid().ToString();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
            services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>();
            var provider = services.BuildServiceProvider();
            var db = provider.GetRequiredService<ApplicationDbContext>();
            db.Users.AddRange(users);
            db.SaveChanges();
            return provider;
        }

        private static ProfileController CreateController(ServiceProvider services, string? userId = null)
        {
            var identity = new ClaimsIdentity();
            if (userId != null)
            {
                identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "Test");
            }
            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            return new ProfileController(
                services.GetRequiredService<ApplicationDbContext>(),
                services.GetRequiredService<UserManager<ApplicationUser>>())
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
            };
        }

        private static ApplicationUser NewUser(string id) => new ApplicationUser
        {
            Id = id,
            UserName = id,
            Email = id + "@example.com",
            DisplayName = "Display " + id,
            AvatarUrl = "/img/" + id + ".png"
        };

        private static Recipe NewRecipe(int id, string userId) => new Recipe
        {
            Id = id,
            UserId = userId,
            Name = "Recipe " + id,
            Description = "Description",
            Category = "Món Việt",
            Ingredients = "Ingredients",
            Instructions = "Instructions",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        private sealed class TestTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }
    }
}
