using System.Security.Claims;
using CookShare.Controllers;
using CookShare.Models;
using CookShare.Tests.Services;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Tests.Controllers
{
    public sealed class SocialContentControllerTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HiddenPostOrParent_BlocksDetailsEditingAndDirectMedia(bool hidePost)
        {
            await using var fixture = await SocialTestDatabase.CreateAsync();
            int id = await fixture.Service().CreateAsync("cook", fixture.RecipeId, "", Guid.NewGuid(), SocialTestDatabase.Image());
            var post = await fixture.Db.Set<CookedRecipePost>().SingleAsync();
            string fileName = post.ImageFileName;
            if (hidePost)
            {
                post.IsHidden = true;
            }
            else
            {
                (await fixture.Db.Recipes.SingleAsync()).IsHidden = true;
            }
            await fixture.Db.SaveChangesAsync();
            var posts = Setup(new CookedPostsController(fixture.Db, fixture.Service()), "cook");
            var images = Setup(new CookedPostImagesController(fixture.Db, fixture.Storage), null);

            Assert.IsType<NotFoundResult>(await posts.Details(id));
            Assert.IsType<NotFoundResult>(await posts.Edit(id));
            Assert.IsType<NotFoundResult>(await images.Get(id, fileName));
            Assert.IsType<NotFoundResult>(await posts.Edit(id, new CookedPostFormViewModel { Note = "bypass" }));
        }

        [Fact]
        public async Task PublicMedia_RequiresMatchingPostAndFilename_AndDisablesCaching()
        {
            await using var fixture = await SocialTestDatabase.CreateAsync();
            int id = await fixture.Service().CreateAsync("cook", fixture.RecipeId, "", Guid.NewGuid(), SocialTestDatabase.Image());
            string fileName = Assert.Single(fixture.Storage.Files.Keys);
            var controller = Setup(new CookedPostImagesController(fixture.Db, fixture.Storage), null);

            Assert.IsType<NotFoundResult>(await controller.Get(id + 1, fileName));
            Assert.IsType<NotFoundResult>(await controller.Get(id, "other.png"));
            var result = Assert.IsType<FileStreamResult>(await controller.Get(id, fileName));
            await result.FileStream.DisposeAsync();
            Assert.Equal("no-store", controller.Response.Headers.CacheControl);
            Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"]);
        }

        [Fact]
        public async Task ForeignOwner_CannotEditOrDeleteThroughController()
        {
            await using var fixture = await SocialTestDatabase.CreateAsync();
            int id = await fixture.Service().CreateAsync("cook", fixture.RecipeId, "", Guid.NewGuid(), SocialTestDatabase.Image());
            var controller = Setup(new CookedPostsController(fixture.Db, fixture.Service()), "owner");
            Assert.IsType<ForbidResult>(await controller.Edit(id));
            Assert.IsType<ForbidResult>(await controller.Edit(id, new CookedPostFormViewModel { Note = "bypass" }));
            Assert.IsType<ForbidResult>(await controller.Delete(id));
            Assert.Single(fixture.Storage.Files);
        }

        [Fact]
        public async Task DuplicateReports_ProduceOneRecord_AndHiddenTargetCannotBeReported()
        {
            await using var fixture = await SocialTestDatabase.CreateAsync();
            var controller = Setup(new ContentReportsController(fixture.Db), "cook");
            var model = new ContentReportFormViewModel
            {
                TargetType = ContentTargetType.Recipe,
                TargetId = fixture.RecipeId,
                Reason = ReportReason.UnsafeContent,
                Details = "Kiểm tra lại"
            };
            Assert.IsType<RedirectToActionResult>(await controller.Create(model));
            Assert.IsType<RedirectToActionResult>(await controller.Create(model));
            Assert.Single(await fixture.Db.Set<ContentReport>().ToListAsync());
            (await fixture.Db.Recipes.SingleAsync()).IsHidden = true;
            await fixture.Db.SaveChangesAsync();
            Assert.IsType<NotFoundResult>(await controller.Create(model));
        }

        [Fact]
        public async Task OrdinaryUser_CannotReadQueueOrMakeDirectModerationDecision()
        {
            await using var fixture = await SocialTestDatabase.CreateAsync();
            var controller = Setup(new ModerationController(fixture.Db), "owner");
            Assert.IsType<ForbidResult>(await controller.Index());
            Assert.IsType<ForbidResult>(await controller.Details(1));
            Assert.IsType<ForbidResult>(await controller.Decide(1, new ModerationDecisionViewModel
            {
                Action = ModerationActionType.Hide,
                Reason = "Tamper"
            }));
            var authorization = Assert.Single(typeof(ModerationController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
            Assert.Equal("Moderator", authorization.Roles);
            Assert.Empty(await fixture.Db.Set<ModerationAction>().ToListAsync());
        }

        [Theory]
        [InlineData(ContentTargetType.Recipe)]
        [InlineData(ContentTargetType.CookedPost)]
        public async Task Moderator_HideRestoreAndDuplicateDecision_KeepAuditAndVisibilityConsistent(ContentTargetType targetType)
        {
            await using var fixture = await SocialTestDatabase.CreateAsync();
            int postId = await fixture.Service().CreateAsync("cook", fixture.RecipeId, "", Guid.NewGuid(), SocialTestDatabase.Image());
            string fileName = Assert.Single(fixture.Storage.Files.Keys);
            var report = new ContentReport
            {
                ReporterId = "cook",
                TargetType = targetType,
                TargetId = targetType == ContentTargetType.Recipe ? fixture.RecipeId : postId,
                Reason = ReportReason.Spam
            };
            fixture.Db.Set<ContentReport>().Add(report);
            await fixture.Db.SaveChangesAsync();
            var moderator = Setup(new ModerationController(fixture.Db), "owner", moderator: true);
            var images = Setup(new CookedPostImagesController(fixture.Db, fixture.Storage), null);
            var hide = new ModerationDecisionViewModel { Action = ModerationActionType.Hide, Reason = "Ẩn để kiểm tra" };

            Assert.IsType<RedirectToActionResult>(await moderator.Decide(report.Id, hide));
            Assert.IsType<RedirectToActionResult>(await moderator.Decide(report.Id, hide));
            Assert.IsType<NotFoundResult>(await images.Get(postId, fileName));
            Assert.Empty(await fixture.Db.Set<CookedRecipePost>().AsNoTracking().ToListAsync());
            Assert.Single(await fixture.Db.Set<ModerationAction>().ToListAsync());
            Assert.Equal(ReportStatus.Hidden, report.Status);
            await moderator.Decide(report.Id, new ModerationDecisionViewModel { Action = ModerationActionType.Restore, Reason = "Đã xác minh phù hợp" });

            Assert.Single(await fixture.Db.Set<CookedRecipePost>().AsNoTracking().ToListAsync());
            var image = Assert.IsType<FileStreamResult>(await images.Get(postId, fileName));
            await image.FileStream.DisposeAsync();
            var audit = await fixture.Db.Set<ModerationAction>().OrderBy(action => action.Id).ToListAsync();
            Assert.Equal(new[] { ModerationActionType.Hide, ModerationActionType.Restore }, audit.Select(action => action.Action));
            Assert.All(audit, action => Assert.Equal("owner", action.ModeratorId));
            Assert.Equal(ReportStatus.Restored, report.Status);
        }

        [Fact]
        public async Task AuditFailure_RollsBackContentVisibilityAndReportStatus()
        {
            var interceptor = new SocialSaveFailureInterceptor();
            await using var fixture = await SocialTestDatabase.CreateAsync(interceptor);
            var report = new ContentReport { ReporterId = "cook", TargetType = ContentTargetType.Recipe, TargetId = fixture.RecipeId };
            fixture.Db.Set<ContentReport>().Add(report);
            await fixture.Db.SaveChangesAsync();
            interceptor.RejectAudit = true;
            var controller = Setup(new ModerationController(fixture.Db), "owner", moderator: true);

            await controller.Decide(report.Id, new ModerationDecisionViewModel { Action = ModerationActionType.Hide, Reason = "Kiểm tra" });

            fixture.Db.ChangeTracker.Clear();
            Assert.False((await fixture.Db.Recipes.IgnoreQueryFilters().SingleAsync()).IsHidden);
            Assert.Equal(ReportStatus.Open, (await fixture.Db.Set<ContentReport>().SingleAsync()).Status);
            Assert.Empty(await fixture.Db.Set<ModerationAction>().ToListAsync());
        }

        [Fact]
        public async Task DismissDeletedTarget_PreservesAuditIdentity()
        {
            await using var fixture = await SocialTestDatabase.CreateAsync();
            var report = new ContentReport { ReporterId = "cook", TargetType = ContentTargetType.Recipe, TargetId = fixture.RecipeId };
            fixture.Db.Set<ContentReport>().Add(report);
            await fixture.Db.SaveChangesAsync();
            fixture.Db.Recipes.Remove(await fixture.Db.Recipes.SingleAsync());
            await fixture.Db.SaveChangesAsync();
            var controller = Setup(new ModerationController(fixture.Db), "owner", moderator: true);

            await controller.Decide(report.Id, new ModerationDecisionViewModel { Action = ModerationActionType.Dismiss, Reason = "Nội dung đã xóa" });
            var audit = await fixture.Db.Set<ModerationAction>().SingleAsync();
            Assert.Equal(fixture.RecipeId, audit.TargetId);
            Assert.Equal(ReportStatus.Dismissed, report.Status);
        }

        private static T Setup<T>(T controller, string? userId, bool moderator = false) where T : Controller
        {
            var identity = new ClaimsIdentity(userId == null ? null : "Test");
            if (userId != null)
            {
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
            }
            if (moderator)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, "Moderator"));
            }
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            controller.ControllerContext = new ControllerContext { HttpContext = context };
            controller.TempData = new TempDataDictionary(context, new MemoryTempDataProvider());
            return controller;
        }

        private sealed class MemoryTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }
    }
}
