using System.Reflection;
using System.Security.Claims;
using CookShare.Controllers;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Tests.Controllers
{
    public class RecipeControllerTests
    {
        [Fact]
        public async Task Index_ShouldReturnNewestRecipesWithAuthors()
        {
            var author = new ApplicationUser { Id = "owner-id", DisplayName = "Recipe author" };
            var older = NewRecipe(1);
            older.User = author;
            older.CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var newer = NewRecipe(2);
            newer.User = author;
            newer.CreatedAt = older.CreatedAt.AddDays(1);
            using var db = CreateDb(older, newer);
            db.ChangeTracker.Clear();
            var controller = CreateController(db);

            var result = Assert.IsType<ViewResult>(await controller.Index());
            var recipes = Assert.IsAssignableFrom<IEnumerable<Recipe>>(result.Model).ToList();

            Assert.Equal(new[] { 2, 1 }, recipes.Select(recipe => recipe.Id));
            Assert.All(recipes, recipe => Assert.Equal(author.DisplayName, recipe.User?.DisplayName));
        }

        [Fact]
        public async Task Index_EmptyDatabase_ShouldReturnEmptyList()
        {
            using var db = CreateDb();
            var controller = CreateController(db);

            var result = Assert.IsType<ViewResult>(await controller.Index());

            Assert.Empty(Assert.IsAssignableFrom<IEnumerable<Recipe>>(result.Model));
        }

        [Fact]
        public void Create_ShouldReturnEmptyFormModel()
        {
            using var db = CreateDb();
            var controller = CreateController(db);

            var result = Assert.IsType<ViewResult>(controller.Create());
            var model = Assert.IsType<RecipeFormViewModel>(result.Model);

            Assert.Equal(string.Empty, model.Name);
        }

        [Fact]
        public async Task Create_ValidForm_ShouldPersistRecipeForCurrentUser()
        {
            using var db = CreateDb();
            var controller = CreateController(db);
            var model = NewForm();

            var result = Assert.IsType<RedirectToActionResult>(await controller.Create(model));
            db.ChangeTracker.Clear();
            var recipe = Assert.Single(await db.Recipes.ToListAsync());

            AssertRecipeMatches(model, recipe);
            Assert.Equal("owner-id", recipe.UserId);
            Assert.Equal(DateTimeKind.Utc, recipe.CreatedAt.Kind);
            Assert.Equal(nameof(RecipeController.Details), result.ActionName);
            Assert.Equal(recipe.Id, result.RouteValues!["id"]);
            Assert.NotNull(controller.TempData["Success"]);
        }

        [Fact]
        public async Task Create_InvalidForm_ShouldRedisplayModelWithoutSaving()
        {
            using var db = CreateDb();
            var controller = CreateController(db);
            var model = NewForm();
            // MVC validation runs before the action; direct calls must set ModelState.
            controller.ModelState.AddModelError(nameof(model.Name), "Required");

            var result = Assert.IsType<ViewResult>(await controller.Create(model));

            Assert.Same(model, result.Model);
            Assert.Empty(await db.Recipes.ToListAsync());
        }

        [Fact]
        public async Task Create_UserWithoutId_ShouldChallengeWithoutSaving()
        {
            using var db = CreateDb();
            var controller = CreateController(db, null);

            Assert.IsType<ChallengeResult>(await controller.Create(NewForm()));
            Assert.Empty(await db.Recipes.ToListAsync());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public async Task Edit_Owner_ShouldReturnRecipeFormAndId(int id)
        {
            var recipe = NewRecipe(id);
            using var db = CreateDb(recipe);
            var controller = CreateController(db);

            var result = Assert.IsType<ViewResult>(await controller.Edit(id));
            var model = Assert.IsType<RecipeFormViewModel>(result.Model);

            Assert.Equal(id, controller.ViewData["RecipeId"]);
            AssertRecipeMatches(model, recipe);
        }

        [Fact]
        public async Task Edit_ValidForm_ShouldPersistChangesAndPreserveOwnerAndCreationDate()
        {
            var recipe = NewRecipe(1);
            var createdAt = recipe.CreatedAt;
            using var db = CreateDb(recipe);
            var controller = CreateController(db);
            var model = NewForm();

            var result = Assert.IsType<RedirectToActionResult>(await controller.Edit(recipe.Id, model));
            db.ChangeTracker.Clear();
            var updated = await db.Recipes.SingleAsync();

            AssertRecipeMatches(model, updated);
            Assert.Equal("owner-id", updated.UserId);
            Assert.Equal(createdAt, updated.CreatedAt);
            Assert.Equal(nameof(RecipeController.Details), result.ActionName);
            Assert.Equal(recipe.Id, result.RouteValues!["id"]);
            Assert.NotNull(controller.TempData["Success"]);
        }

        [Fact]
        public async Task Edit_InvalidForm_ShouldRedisplayModelWithoutChangingRecipe()
        {
            var recipe = NewRecipe(1);
            var originalName = recipe.Name;
            using var db = CreateDb(recipe);
            var controller = CreateController(db);
            var model = NewForm();
            controller.ModelState.AddModelError(nameof(model.Name), "Required");

            var result = Assert.IsType<ViewResult>(await controller.Edit(recipe.Id, model));

            Assert.Same(model, result.Model);
            Assert.Equal(recipe.Id, controller.ViewData["RecipeId"]);
            Assert.Equal(originalName, recipe.Name);
            db.ChangeTracker.Clear();
            Assert.Equal(originalName, (await db.Recipes.SingleAsync()).Name);
        }

        [Theory]
        [InlineData("another-user-id", "owner-id")]
        [InlineData(null, "owner-id")]
        [InlineData("owner-id", null)]
        [InlineData(null, null)]
        public async Task EditAndDelete_WithoutOwnership_ShouldForbidAndLeaveRecipeUnchanged(
            string? currentUserId, string? recipeUserId)
        {
            var recipe = NewRecipe(1);
            recipe.UserId = recipeUserId;
            var originalName = recipe.Name;
            using var db = CreateDb(recipe);
            var controller = CreateController(db, currentUserId);

            Assert.IsType<ForbidResult>(await controller.Edit(recipe.Id));
            Assert.IsType<ForbidResult>(await controller.Edit(recipe.Id, NewForm()));
            Assert.IsType<ForbidResult>(await controller.Delete(recipe.Id));

            Assert.Equal(originalName, recipe.Name);
            db.ChangeTracker.Clear();
            Assert.Equal(originalName, (await db.Recipes.SingleAsync()).Name);
        }

        [Fact]
        public async Task MissingRecipe_ShouldReturnNotFound()
        {
            using var db = CreateDb();
            var controller = CreateController(db);

            Assert.IsType<NotFoundResult>(await controller.Details(1));
            Assert.IsType<NotFoundResult>(await controller.Edit(1));
            Assert.IsType<NotFoundResult>(await controller.Edit(1, NewForm()));
            Assert.IsType<NotFoundResult>(await controller.Delete(1));
        }

        // Direct controller calls do not execute MVC authorization filters.
        [Theory]
        [InlineData(nameof(RecipeController.Create))]
        [InlineData(nameof(RecipeController.Saved))]
        [InlineData(nameof(RecipeController.Edit))]
        [InlineData(nameof(RecipeController.Delete))]
        public void PrivateActions_AllOverloads_ShouldRequireAuthorization(string actionName)
        {
            var actions = typeof(RecipeController).GetMethods().Where(method => method.Name == actionName).ToList();

            Assert.NotEmpty(actions);
            Assert.All(actions, action =>
            {
                Assert.NotNull(action.GetCustomAttribute<AuthorizeAttribute>());
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            });
        }

        [Fact]
        public void MutatingActions_ShouldRequirePostAndAntiForgeryToken()
        {
            var controllerType = typeof(RecipeController);
            var actions = new[]
            {
                controllerType.GetMethod(nameof(RecipeController.Create), new[] { typeof(RecipeFormViewModel) })!,
                controllerType.GetMethod(nameof(RecipeController.Edit), new[] { typeof(int), typeof(RecipeFormViewModel) })!,
                controllerType.GetMethod(nameof(RecipeController.Delete), new[] { typeof(int) })!
            };

            Assert.All(actions, action =>
            {
                Assert.NotNull(action.GetCustomAttribute<HttpPostAttribute>());
                Assert.Null(action.GetCustomAttribute<HttpGetAttribute>());
                Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            });
        }

        [Theory]
        [InlineData(nameof(RecipeController.Index), 0)]
        [InlineData(nameof(RecipeController.Details), 1)]
        [InlineData(nameof(RecipeController.Create), 0)]
        [InlineData(nameof(RecipeController.Edit), 1)]
        [InlineData(nameof(RecipeController.Saved), 0)]
        public void ReadActions_ShouldRequireGet(string actionName, int parameterCount)
        {
            var action = typeof(RecipeController).GetMethods().Single(method =>
                method.Name == actionName && method.GetParameters().Length == parameterCount);

            Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
            Assert.Null(action.GetCustomAttribute<HttpPostAttribute>());
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
        public async Task Details_ShouldReturnRecipeWithAuthor(int id)
        {
            var recipe = NewRecipe(id);
            recipe.User = new ApplicationUser { Id = "owner-id", DisplayName = "Recipe author" };
            using var db = CreateDb(recipe);
            db.ChangeTracker.Clear();
            var controller = CreateController(db);

            var result = Assert.IsType<ViewResult>(await controller.Details(id));
            var model = Assert.IsType<Recipe>(result.Model);

            Assert.Equal(id, model.Id);
            Assert.Equal(recipe.Name, model.Name);
            Assert.Equal(recipe.User.DisplayName, model.User?.DisplayName);
        }

        [Fact]
        public async Task Delete_Owner_ShouldRemoveOnlyRequestedRecipe()
        {
            using var db = CreateDb(NewRecipe(1), NewRecipe(2));
            var controller = CreateController(db);

            var result = Assert.IsType<RedirectToActionResult>(await controller.Delete(1));
            db.ChangeTracker.Clear();
            var remaining = Assert.Single(await db.Recipes.ToListAsync());

            Assert.Equal(2, remaining.Id);
            Assert.Equal(nameof(RecipeController.Index), result.ActionName);
            Assert.NotNull(controller.TempData["Success"]);
        }

        [Fact]
        public void Saved_ShouldReturnView()
        {
            using var db = CreateDb();
            var controller = CreateController(db);

            Assert.IsType<ViewResult>(controller.Saved());
        }

        private static ApplicationDbContext CreateDb(params Recipe[] recipes)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new ApplicationDbContext(options);
            db.Recipes.AddRange(recipes);
            db.SaveChanges();
            return db;
        }

        private static RecipeController CreateController(ApplicationDbContext db, string? userId = "owner-id")
        {
            var identity = new ClaimsIdentity("Test");
            if (userId != null)
            {
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
            }

            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            return new RecipeController(db)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
            };
        }

        private static Recipe NewRecipe(int id) => new Recipe
        {
            Id = id,
            UserId = "owner-id",
            Name = "Original recipe",
            Category = "Món Việt",
            Description = "Original description",
            Ingredients = "Original ingredients",
            Instructions = "Original instructions",
            Image = "/img/recipe-placeholder.svg"
        };

        private static RecipeFormViewModel NewForm() => new RecipeFormViewModel
        {
            Name = "  Updated recipe  ",
            Category = "  Món chay  ",
            Description = "  Updated description  ",
            Ingredients = "  Updated ingredients  ",
            Instructions = "  Updated instructions  ",
            Image = "/img/recipes/salad.webp"
        };

        private static void AssertRecipeMatches(RecipeFormViewModel model, Recipe recipe)
        {
            Assert.Equal(model.Name.Trim(), recipe.Name);
            Assert.Equal(model.Category.Trim(), recipe.Category);
            Assert.Equal(model.Description.Trim(), recipe.Description);
            Assert.Equal(model.Ingredients.Trim(), recipe.Ingredients);
            Assert.Equal(model.Instructions.Trim(), recipe.Instructions);
            Assert.Equal(model.Image, recipe.Image);
        }

        private sealed class TestTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }
    }
}
