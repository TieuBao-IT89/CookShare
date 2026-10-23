namespace CookShare.ViewModels
{
    public class NotificationViewModel
    {
        public int Id { get; set; }

        public string ActorName { get; set; } = string.Empty;

        public string? ActorAvatarUrl { get; set; }

        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool HasRecipe { get; set; }
    }
}
