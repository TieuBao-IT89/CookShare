using System.Diagnostics.CodeAnalysis;
using CookShare.Models.Identity;

namespace CookShare.Models
{
    [SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Collection is the domain name for a user's recipe board, not a collection implementation.")]
    public sealed class RecipeCollection
    {
        public int Id { get; set; }

        public string OwnerId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsPublic { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser Owner { get; set; } = null!;

        public ICollection<CollectionRecipe> Recipes { get; set; } = new List<CollectionRecipe>();
    }
}
