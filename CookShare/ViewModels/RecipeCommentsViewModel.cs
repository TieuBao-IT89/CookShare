namespace CookShare.ViewModels
{
    public class RecipeCommentsViewModel
    {
        public int RecipeId { get; set; }

        public IReadOnlyList<RecipeCommentViewModel> Comments { get; set; } = Array.Empty<RecipeCommentViewModel>();

        public bool IsAuthenticated { get; set; }
    }
}
