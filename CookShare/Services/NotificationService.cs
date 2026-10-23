using CookShare.Data;
using CookShare.Models;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _db;

        public NotificationService(ApplicationDbContext db)
        {
            _db = db;
        }

        public Task NotifyFollowAsync(Follow follow)
        {
            return AddAsync(
                follow.FollowingId,
                follow.FollowerId,
                "Follow",
                "đã bắt đầu theo dõi bạn.",
                $"follow:{follow.Id}");
        }

        public Task NotifyRecipeLikeAsync(RecipeLike like)
        {
            var recipientId = like.Recipe.UserId;
            if (string.IsNullOrEmpty(recipientId))
            {
                return Task.CompletedTask;
            }

            return AddAsync(
                recipientId,
                like.UserId,
                "Like",
                $"đã thích công thức \"{like.Recipe.Name}\" của bạn.",
                $"recipe-like:{like.Id}",
                like.RecipeId);
        }

        public Task NotifyRecipeCommentAsync(RecipeComment comment)
        {
            var recipientId = comment.Recipe.UserId;
            if (string.IsNullOrEmpty(recipientId))
            {
                return Task.CompletedTask;
            }

            return AddAsync(
                recipientId,
                comment.UserId,
                "Comment",
                $"đã bình luận về công thức \"{comment.Recipe.Name}\" của bạn.",
                $"recipe-comment:{comment.Id}",
                comment.RecipeId,
                comment.Id);
        }

        private async Task AddAsync(
            string recipientId,
            string actorId,
            string type,
            string message,
            string eventKey,
            int? recipeId = null,
            int? commentId = null)
        {
            if (string.IsNullOrWhiteSpace(recipientId)
                || string.IsNullOrWhiteSpace(actorId)
                || recipientId == actorId
                || await _db.Notifications.AnyAsync(
                    notification => notification.EventKey == eventKey))
            {
                return;
            }

            _db.Notifications.Add(new Notification
            {
                RecipientId = recipientId,
                ActorId = actorId,
                Type = type,
                Message = message,
                EventKey = eventKey,
                RecipeId = recipeId,
                CommentId = commentId,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }
    }
}
