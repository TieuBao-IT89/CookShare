using System.Reflection;
using System.Security.Claims;
using CookShare.Controllers;
using CookShare.Data;
using CookShare.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Tests.Controllers
{
    public class RecipeControllerTests
    {
        [Fact]
        public void Index_ShouldReturnView()
        {
            // Arrange
            var controller = new RecipeController();

            // Act
            var result = controller.Index();

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public void Create_ShouldReturnView()
        {
            // Arrange
            var controller = new RecipeController();

            // Act
            var result = controller.Create();

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public async Task Edit_Owner_ShouldSetCorrectRecipeId(int id)
        {
            // Arrange
            var controller = CreateAuthenticatedController("owner-id");
            using var db = new RecipeTestDbContext(new Recipe { Id = id, UserId = "owner-id" });

            // Act
            var result = await controller.Edit(id, db);

            // Assert
            Assert.IsType<ViewResult>(result);
            Assert.Equal(id, controller.ViewData["RecipeId"]);
        }

        [Fact]
        public async Task Edit_MissingRecipe_ShouldReturnNotFound()
        {
            var controller = CreateAuthenticatedController("owner-id");
            using var db = new RecipeTestDbContext(null);

            var result = await controller.Edit(1, db);

            Assert.IsType<NotFoundResult>(result);
        }

        [Theory]
        [InlineData("another-user-id")]
        [InlineData(null)]
        public async Task Edit_OtherOwnerOrLegacyRecipe_ShouldForbid(string? recipeUserId)
        {
            var controller = CreateAuthenticatedController("owner-id");
            using var db = new RecipeTestDbContext(new Recipe { Id = 1, UserId = recipeUserId });

            var result = await controller.Edit(1, db);

            Assert.IsType<ForbidResult>(result);
        }

        [Theory]
        [InlineData("owner-id")]
        [InlineData(null)]
        public async Task Edit_AuthenticatedUserWithoutId_ShouldForbid(string? recipeUserId)
        {
            var controller = CreateAuthenticatedController(null);
            using var db = new RecipeTestDbContext(new Recipe { Id = 1, UserId = recipeUserId });

            var result = await controller.Edit(1, db);

            Assert.IsType<ForbidResult>(result);
        }

        // Direct controller calls do not execute MVC authorization filters.
        [Theory]
        [InlineData(nameof(RecipeController.Create))]
        [InlineData(nameof(RecipeController.Saved))]
        [InlineData(nameof(RecipeController.Edit))]
        public void PrivateAction_ShouldRequireAuthorization(string actionName)
        {
            var action = typeof(RecipeController).GetMethod(actionName)!;

            Assert.NotNull(action.GetCustomAttribute<AuthorizeAttribute>());
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Theory]
        [InlineData(nameof(RecipeController.Index))]
        [InlineData(nameof(RecipeController.Details))]
        public void PublicAction_ShouldNotRequireAuthorization(string actionName)
        {
            var action = typeof(RecipeController).GetMethod(actionName)!;

            Assert.Null(typeof(RecipeController).GetCustomAttribute<AuthorizeAttribute>());
            Assert.Null(action.GetCustomAttribute<AuthorizeAttribute>());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void Details_ShouldSetCorrectRecipeId(int id)
        {
            // Arrange
            var controller = new RecipeController();

            // Act
            var result = controller.Details(id);

            // Assert
            Assert.IsType<ViewResult>(result);
            Assert.Equal(id, controller.ViewData["RecipeId"]);
        }

        [Fact]
        public void Saved_ShouldReturnView()
        {
            // Arrange
            var controller = new RecipeController();

            // Act
            var result = controller.Saved();

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        private static RecipeController CreateAuthenticatedController(string? userId)
        {
            var identity = new ClaimsIdentity("Test");
            if (userId != null)
            {
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
            }

            return new RecipeController
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
                }
            };
        }

        // Stub lookup results without connecting to the application's database.
        private sealed class RecipeTestDbContext : ApplicationDbContext
        {
            private readonly DbSet<Recipe> _recipes;

            public RecipeTestDbContext(Recipe? recipe)
                : base(new DbContextOptionsBuilder<ApplicationDbContext>().Options)
            {
                _recipes = new RecipeTestDbSet(recipe);
            }

            public override DbSet<TEntity> Set<TEntity>()
            {
                return typeof(TEntity) == typeof(Recipe)
                    ? (DbSet<TEntity>)(object)_recipes
                    : base.Set<TEntity>();
            }
        }

        private sealed class RecipeTestDbSet : DbSet<Recipe>
        {
            private readonly Recipe? _recipe;

            public override Microsoft.EntityFrameworkCore.Metadata.IEntityType EntityType
                => throw new NotSupportedException("This test double only supports FindAsync.");

            public RecipeTestDbSet(Recipe? recipe)
            {
                _recipe = recipe;
            }

            public override ValueTask<Recipe?> FindAsync(params object?[]? keyValues)
            {
                var recipe = keyValues is [int id] && _recipe?.Id == id ? _recipe : null;
                return ValueTask.FromResult(recipe);
            }
        }
    }
}
