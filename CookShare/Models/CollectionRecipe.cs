namespace CookShare.Models
{
    public sealed class CollectionRecipe
    {
        public int CollectionId { get; set; }

        public int RecipeId { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public RecipeCollection Collection { get; set; } = null!;

        public Recipe Recipe { get; set; } = null!;
    }
}
