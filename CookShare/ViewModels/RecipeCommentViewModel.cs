namespace CookShare.ViewModels
{
    public class RecipeCommentViewModel
    {
        public int Id { get; set; }

        public int RecipeId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string AuthorName { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public bool CanManage { get; set; }
    }
}
