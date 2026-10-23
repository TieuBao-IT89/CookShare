using System.Security.Claims;
using CookShare.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Controllers
{
    public class NotificationBadgeViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _db;

        public NotificationBadgeViewComponent(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = UserClaimsPrincipal.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Content(string.Empty);
            }

            const string unreadCountItemKey = "CookShare.NotificationUnreadCount";
            if (HttpContext.Items.TryGetValue(unreadCountItemKey, out var cached)
                && cached is int cachedCount)
            {
                return View(cachedCount);
            }

            var unreadCount = await _db.Notifications
                .CountAsync(notification =>
                    notification.RecipientId == userId
                    && !notification.IsRead);
            HttpContext.Items[unreadCountItemKey] = unreadCount;

            return View(unreadCount);
        }
    }
}
