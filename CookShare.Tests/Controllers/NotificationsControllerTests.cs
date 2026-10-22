using System.Security.Claims;
using CookShare.Controllers;
using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Tests.Controllers
{
    public class NotificationsControllerTests
    {
        [Fact]
        public async Task Index_ReturnsOnlyCurrentUsersNotifications()
        {
            await using var db = CreateDb();
            var own = NewNotification("owner", "actor", 1);
            var other = NewNotification("other", "actor", 2);
            db.Users.AddRange(
                new ApplicationUser { Id = "owner", UserName = "owner" },
                new ApplicationUser { Id = "other", UserName = "other" },
                new ApplicationUser { Id = "actor", UserName = "actor" });
            db.Notifications.AddRange(own, other);
            await db.SaveChangesAsync();

            var result = Assert.IsType<ViewResult>(
                await CreateController(db, "owner").Index());

            var notifications = Assert.IsAssignableFrom<
                IEnumerable<CookShare.ViewModels.NotificationViewModel>>(
                    result.Model);
            Assert.Equal(own.Id, Assert.Single(notifications).Id);
        }

        [Fact]
        public async Task Open_CannotAccessAnotherUsersNotification()
        {
            await using var db = CreateDb();
            db.Users.AddRange(
                new ApplicationUser { Id = "owner" },
                new ApplicationUser { Id = "other" },
                new ApplicationUser { Id = "actor" });
            var notification = NewNotification("other", "actor", 1);
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();

            var result = await CreateController(db, "owner")
                .Open(notification.Id);

            Assert.IsType<NotFoundResult>(result);
            Assert.False((await db.Notifications.SingleAsync()).IsRead);
        }

        [Fact]
        public async Task MarkAsRead_UpdatesOnlyOwnedNotification()
        {
            await using var db = CreateDb();
            db.Users.AddRange(
                new ApplicationUser { Id = "owner" },
                new ApplicationUser { Id = "actor" });
            var notification = NewNotification("owner", "actor", 1);
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();

            var result = await CreateController(db, "owner")
                .MarkAsRead(notification.Id);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.True((await db.Notifications.SingleAsync()).IsRead);
        }

        private static ApplicationDbContext CreateDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private static Notification NewNotification(
            string recipientId,
            string actorId,
            int id) => new()
            {
                Id = id,
                RecipientId = recipientId,
                ActorId = actorId,
                Type = "Follow",
                Message = "started following you",
                EventKey = $"follow:{id}",
                CreatedAt = DateTime.UtcNow
            };

        private static NotificationsController CreateController(
            ApplicationDbContext db,
            string userId)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId) },
                "Test");
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            };

            return new NotificationsController(db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                }
            };
        }
    }
}
