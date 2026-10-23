using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using CookShare.Controllers;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CookShare.Tests.Controllers
{
    public class RecipeCommentsControllerTests
    {
        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public async Task Index_PublicRead_ShouldOrderCommentsAndProjectOnlyPublicAuthorData()
        {
            using var db = CreateDb();
            AddComments(db);
            db.ChangeTracker.Clear();
            var controller = CreateController(db, null);

            JsonElement body = JsonBody(await controller.Index(1));

            Assert.Equal(3, body.GetProperty("count").GetInt32());
            JsonElement[] comments = body.GetProperty("comments").EnumerateArray().ToArray();
            Assert.Equal(new[] { 3, 2, 1 }, comments.Select(comment => comment.GetProperty("id").GetInt32()));
            Assert.All(comments, comment => Assert.False(comment.GetProperty("canManage").GetBoolean()));
            Assert.Equal("Người viết", comments[2].GetProperty("authorName").GetString());
            Assert.DoesNotContain("private@example.test", body.GetRawText());
            Assert.DoesNotContain("passwordHash", body.GetRawText());
            Assert.Empty(db.ChangeTracker.Entries());
        }

        [Fact]
        public async Task LoadAsync_ShouldUseIdentityForOwnershipAndUsernameFallback()
        {
            using var db = CreateDb();
            AddComments(db);

            List<RecipeCommentViewModel> comments = await RecipeCommentQueries.LoadAsync(db, 1, "owner-id");

            Assert.Equal("second-user", comments[0].AuthorName);
            Assert.False(comments[0].CanManage);
            Assert.All(comments.Where(comment => comment.UserId == "owner-id"), comment => Assert.True(comment.CanManage));
        }

        [Fact]
        public void CommentProjection_ShouldTranslateToPostgresWithoutLoadingPrivateUserColumns()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=translation_only;Username=translation_only")
                .Options;
            using var db = new ApplicationDbContext(options);

            string sql = RecipeCommentQueries.ForRecipe(db, 1, "owner-id").ToQueryString();

            Assert.Contains("RecipeComments", sql);
            Assert.Contains("AspNetUsers", sql);
            Assert.DoesNotContain("PasswordHash", sql);
            Assert.DoesNotContain("SecurityStamp", sql);
            Assert.DoesNotContain("Email", sql);
        }

        [Fact]
        public async Task Create_ShouldPersistTrimmedContentWithRealIdentityAndGeneratedId()
        {
            using var db = CreateDb();
            var controller = CreateController(db);

            JsonElement body = JsonBody(await controller.Create(1, new CommentFormViewModel { Content = "  Rất ngon!  " }));
            db.ChangeTracker.Clear();
            RecipeComment comment = await db.RecipeComments.SingleAsync();

            Assert.Equal("Rất ngon!", comment.Content);
            Assert.Equal("owner-id", comment.UserId);
            Assert.Equal(1, comment.RecipeId);
            Assert.Equal(DateTimeKind.Utc, comment.CreatedAt.Kind);
            Assert.Equal(comment.Id, body.GetProperty("comment").GetProperty("id").GetInt32());
            Assert.Equal("Người viết", body.GetProperty("comment").GetProperty("authorName").GetString());
            Assert.True(body.GetProperty("comment").GetProperty("canManage").GetBoolean());
            Assert.Equal(1, body.GetProperty("count").GetInt32());
        }

        [Theory]
        [InlineData("")]
        [InlineData(" \r\n\t ")]
        public async Task Create_EmptyOrWhitespace_ShouldRejectWithoutSaving(string content)
        {
            using var db = CreateDb();
            var controller = CreateController(db);

            Assert.IsType<BadRequestObjectResult>(await controller.Create(1, new CommentFormViewModel { Content = content }));
            Assert.Empty(await db.RecipeComments.ToListAsync());
        }

        [Theory]
        [InlineData(500, true)]
        [InlineData(501, false)]
        public async Task Create_ShouldEnforceDocumentedCharacterLimit(int length, bool succeeds)
        {
            using var db = CreateDb();
            var controller = CreateController(db);

            IActionResult result = await controller.Create(1, new CommentFormViewModel { Content = new string('a', length) });

            if (succeeds)
            {
                Assert.IsType<JsonResult>(result);
                Assert.Equal(length, (await db.RecipeComments.SingleAsync()).Content.Length);
            }
            else
            {
                Assert.IsType<BadRequestObjectResult>(result);
                Assert.Empty(await db.RecipeComments.ToListAsync());
            }
        }

        [Fact]
        public void Form_ShouldTrimBeforeValidationAndRejectOversizeContent()
        {
            var model = new CommentFormViewModel { Content = "  " + new string('a', 500) + "  " };
            var errors = new List<ValidationResult>();

            Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), errors, true));
            Assert.Equal(500, model.Content.Length);
            model.Content += "a";
            Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors, true));
        }

        [Fact]
        public async Task Create_MvcValidationError_ShouldRejectWithoutSaving()
        {
            using var db = CreateDb();
            var controller = CreateController(db);
            controller.ModelState.AddModelError(nameof(CommentFormViewModel.Content), "Invalid content");

            Assert.IsType<BadRequestObjectResult>(await controller.Create(1, new CommentFormViewModel { Content = "A comment" }));
            Assert.Empty(await db.RecipeComments.ToListAsync());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task Writes_WithoutIdentityId_ShouldChallengeAndKeepComments(string? userId)
        {
            using var db = CreateDb();
            AddComments(db);
            var controller = CreateController(db, userId);

            Assert.IsType<ChallengeResult>(await controller.Create(1, NewForm()));
            Assert.IsType<ChallengeResult>(await controller.Edit(1, 1, NewForm()));
            Assert.IsType<ChallengeResult>(await controller.Delete(1, 1));
            Assert.Equal(3, await db.RecipeComments.CountAsync());
        }

        [Fact]
        public async Task Create_DeletedIdentity_ShouldChallengeWithoutSaving()
        {
            using var db = CreateDb();
            var controller = CreateController(db, "deleted-user-id");

            Assert.IsType<ChallengeResult>(await controller.Create(1, NewForm()));
            Assert.Empty(await db.RecipeComments.ToListAsync());
        }

        [Fact]
        public async Task Edit_Owner_ShouldUpdateSameRowAndPreserveCreatedAt()
        {
            using var db = CreateDb();
            AddComments(db);
            DateTime createdAt = (await db.RecipeComments.FindAsync(1))!.CreatedAt;
            var controller = CreateController(db);

            JsonElement body = JsonBody(await controller.Edit(1, 1, NewForm()));
            db.ChangeTracker.Clear();
            RecipeComment comment = (await db.RecipeComments.FindAsync(1))!;

            Assert.Equal("Nội dung đã thay đổi", comment.Content);
            Assert.Equal(createdAt, comment.CreatedAt);
            Assert.Equal("owner-id", comment.UserId);
            Assert.Equal(3, await db.RecipeComments.CountAsync());
            Assert.Equal(1, body.GetProperty("comment").GetProperty("id").GetInt32());
        }

        [Fact]
        public async Task Edit_InvalidContent_ShouldKeepOriginalContent()
        {
            using var db = CreateDb();
            AddComments(db);
            var controller = CreateController(db);

            Assert.IsType<BadRequestObjectResult>(await controller.Edit(1, 1, new CommentFormViewModel { Content = "   " }));
            db.ChangeTracker.Clear();
            Assert.Equal("First comment", (await db.RecipeComments.FindAsync(1))!.Content);
        }

        [Fact]
        public async Task EditAndDelete_OtherUser_ShouldForbidAndKeepOriginalRow()
        {
            using var db = CreateDb();
            AddComments(db);
            var controller = CreateController(db, "other-id");

            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(await controller.Edit(1, 1, NewForm())).StatusCode);
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(await controller.Delete(1, 1)).StatusCode);
            db.ChangeTracker.Clear();
            Assert.Equal("First comment", (await db.RecipeComments.FindAsync(1))!.Content);
            Assert.Equal(3, await db.RecipeComments.CountAsync());
        }

        [Fact]
        public async Task Delete_Owner_ShouldRemoveOnlyOwnRequestedCommentAndReturnTrustedCount()
        {
            using var db = CreateDb();
            AddComments(db);
            var controller = CreateController(db);

            JsonElement body = JsonBody(await controller.Delete(1, 1));
            db.ChangeTracker.Clear();

            Assert.Null(await db.RecipeComments.FindAsync(1));
            Assert.Equal(new[] { 2, 3 }, await db.RecipeComments.OrderBy(comment => comment.Id).Select(comment => comment.Id).ToArrayAsync());
            Assert.Equal(1, body.GetProperty("commentId").GetInt32());
            Assert.Equal(2, body.GetProperty("count").GetInt32());
        }

        [Fact]
        public async Task MissingRecipeOrComment_ShouldReturnNotFound()
        {
            using var db = CreateDb();
            var controller = CreateController(db);

            Assert.IsType<NotFoundObjectResult>(await controller.Index(99));
            Assert.IsType<NotFoundObjectResult>(await controller.Create(99, NewForm()));
            Assert.IsType<NotFoundObjectResult>(await controller.Edit(1, 99, NewForm()));
            Assert.IsType<NotFoundObjectResult>(await controller.Delete(1, 99));
        }

        [Fact]
        public async Task EditAndDelete_CommentFromDifferentRecipe_ShouldReturnNotFound()
        {
            using var db = CreateDb();
            AddComments(db);
            var controller = CreateController(db);

            Assert.IsType<NotFoundObjectResult>(await controller.Edit(2, 1, NewForm()));
            Assert.IsType<NotFoundObjectResult>(await controller.Delete(2, 1));
            Assert.Equal(3, await db.RecipeComments.CountAsync());
        }

        [Theory]
        [InlineData("create")]
        [InlineData("edit")]
        [InlineData("delete")]
        public async Task Write_SaveFailure_ShouldReturnSafeErrorAndLeaveDatabaseUnchanged(string operation)
        {
            var interceptor = new FailingSaveInterceptor();
            using var db = CreateDb(interceptor);
            AddComments(db);
            interceptor.FailSave = true;
            var controller = CreateController(db);

            IActionResult result = operation switch
            {
                "create" => await controller.Create(1, NewForm()),
                "edit" => await controller.Edit(1, 1, NewForm()),
                _ => await controller.Delete(1, 1)
            };

            var error = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, error.StatusCode);
            Assert.DoesNotContain("sensitive", JsonSerializer.Serialize(error.Value, _jsonOptions));
            db.ChangeTracker.Clear();
            Assert.Equal(3, await db.RecipeComments.CountAsync());
            Assert.Equal("First comment", (await db.RecipeComments.FindAsync(1))!.Content);
        }

        [Fact]
        public void Writes_ShouldRequireAuthenticationPostAndAntiforgery()
        {
            foreach (string actionName in new[] { nameof(RecipeCommentsController.Create), nameof(RecipeCommentsController.Edit), nameof(RecipeCommentsController.Delete) })
            {
                MethodInfo action = typeof(RecipeCommentsController).GetMethod(actionName)!;
                Assert.NotNull(action.GetCustomAttribute<AuthorizeAttribute>());
                Assert.NotNull(action.GetCustomAttribute<HttpPostAttribute>());
                Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
                Assert.Null(action.GetCustomAttribute<HttpGetAttribute>());
            }

            MethodInfo index = typeof(RecipeCommentsController).GetMethod(nameof(RecipeCommentsController.Index))!;
            Assert.NotNull(index.GetCustomAttribute<HttpGetAttribute>());
            Assert.Null(index.GetCustomAttribute<AuthorizeAttribute>());
        }

        private static ApplicationDbContext CreateDb(FailingSaveInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString());
            if (interceptor != null)
            {
                options.AddInterceptors(interceptor);
            }

            var db = new ApplicationDbContext(options.Options);
            db.Users.AddRange(
                new ApplicationUser
                {
                    Id = "owner-id",
                    UserName = "owner-user",
                    DisplayName = "Người viết",
                    Email = "private@example.test",
                    PasswordHash = "private-password-hash",
                    AvatarUrl = "/img/avatar.png"
                },
                new ApplicationUser { Id = "other-id", UserName = "second-user" });
            db.Recipes.AddRange(
                new Recipe { Id = 1, Name = "Món ăn", Category = "Món Việt" },
                new Recipe { Id = 2, Name = "Món khác", Category = "Món Việt" });
            db.SaveChanges();
            return db;
        }

        private static void AddComments(ApplicationDbContext db)
        {
            var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            db.RecipeComments.AddRange(
                new RecipeComment { Id = 1, RecipeId = 1, UserId = "owner-id", Content = "First comment", CreatedAt = createdAt },
                new RecipeComment { Id = 2, RecipeId = 1, UserId = "owner-id", Content = "Second comment", CreatedAt = createdAt.AddDays(1) },
                new RecipeComment { Id = 3, RecipeId = 1, UserId = "other-id", Content = "Third comment", CreatedAt = createdAt.AddDays(1) });
            db.SaveChanges();
        }

        private static RecipeCommentsController CreateController(ApplicationDbContext db, string? userId = "owner-id")
        {
            var identity = new ClaimsIdentity(userId == null ? null : "Test");
            if (userId != null)
            {
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
            }

            return new RecipeCommentsController(db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
                }
            };
        }

        private static CommentFormViewModel NewForm()
        {
            return new CommentFormViewModel { Content = "  Nội dung đã thay đổi  " };
        }

        private static JsonElement JsonBody(IActionResult result)
        {
            return JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value, _jsonOptions);
        }

        private sealed class FailingSaveInterceptor : SaveChangesInterceptor
        {
            public bool FailSave { get; set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                if (FailSave)
                {
                    throw new DbUpdateException("sensitive database detail");
                }

                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }
    }
}
