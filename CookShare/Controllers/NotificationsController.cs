using System.Security.Claims;
using CookShare.Data;
using CookShare.Models;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public NotificationsController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var notifications = await _db.Notifications
                .AsNoTracking()
                .Where(notification => notification.RecipientId == userId)
                .OrderByDescending(notification => notification.CreatedAt)
                .Select(notification => new NotificationViewModel
                {
                    Id = notification.Id,
                    ActorName = notification.Actor.DisplayName
                        ?? notification.Actor.UserName
                        ?? "CookShare User",
                    ActorAvatarUrl = notification.Actor.AvatarUrl,
                    Message = notification.Message ?? string.Empty,
                    EventType = notification.Type,
                    IsRead = notification.IsRead,
                    CreatedAt = notification.CreatedAt,
                    HasRecipe = notification.RecipeId.HasValue
                        && _db.Recipes.Any(recipe => recipe.Id == notification.RecipeId)
                        && (notification.Type != "CookedPost" || _db.CookedRecipePosts.Any(post => post.Id == notification.CookedRecipePostId))
                })
                .ToListAsync();

            ViewBag.UnreadCount = notifications.Count(
                notification => !notification.IsRead);

            return View(notifications);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notification = await GetRecipientNotificationAsync(id);
            if (notification == null)
            {
                return NotFound();
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Open(int id)
        {
            var notification = await GetRecipientNotificationAsync(id);
            if (notification == null)
            {
                return NotFound();
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _db.SaveChangesAsync();
            }

            if (notification.Type == "CookedPost")
            {
                if (notification.CookedRecipePostId.HasValue && await _db.CookedRecipePosts.AnyAsync(post => post.Id == notification.CookedRecipePostId))
                {
                    return RedirectToAction("Details", "CookedPosts", new { id = notification.CookedRecipePostId });
                }
                TempData["StatusMessage"] = "Bài đã nấu thử này không còn hiển thị.";
                return RedirectToAction(nameof(Index));
            }

            if (notification.RecipeId.HasValue)
            {
                if (await _db.Recipes.IgnoreQueryFilters().AnyAsync(recipe => recipe.Id == notification.RecipeId && recipe.IsHidden))
                {
                    TempData["StatusMessage"] = "Công thức này không còn hiển thị.";
                    return RedirectToAction(nameof(Index));
                }
                return RedirectToAction(
                    "Details",
                    "Recipe",
                    new { id = notification.RecipeId.Value },
                    notification.CommentId.HasValue
                        ? $"comment-{notification.CommentId.Value}"
                        : null);
            }

            return RedirectToAction(
                "Index",
                "Profile",
                new { userId = notification.ActorId });
        }

        private Task<Notification?> GetRecipientNotificationAsync(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Task.FromResult<Notification?>(null);
            }

            return _db.Notifications.FirstOrDefaultAsync(
                notification => notification.Id == id
                    && notification.RecipientId == userId);
        }
    }
}
