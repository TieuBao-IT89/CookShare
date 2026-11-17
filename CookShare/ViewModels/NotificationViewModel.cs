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

        public string EventType { get; set; } = string.Empty;
        public string EventLabel => EventType switch
        {
            "CookedPost" => "Đã nấu thử",
            "Follow" => "Theo dõi",
            "Like" => "Yêu thích",
            "Comment" => "Bình luận",
            _ => "Cộng đồng"
        };
    }
}
