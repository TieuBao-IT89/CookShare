namespace CookShare.ViewModels
{
    public sealed class FeedRecipeViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Image { get; set; }
        public string Category { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? AuthorId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public int LikeCount { get; set; }
        public bool IsLiked { get; set; }
        public int CommentCount { get; set; }
        public bool IsSaved { get; set; }
    }

    public sealed class RecipeFeedPage
    {
        public IReadOnlyList<FeedRecipeViewModel> Recipes { get; init; } = Array.Empty<FeedRecipeViewModel>();
        public string Mode { get; init; } = "latest";
        public string? ViewerId { get; init; }
        public string? NextCursor { get; init; }
    }

    public sealed class FeedProfileViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public int RecipeCount { get; set; }
        public int FollowingCount { get; set; }
    }
}
