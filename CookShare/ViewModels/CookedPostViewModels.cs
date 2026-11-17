using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CookShare.ViewModels
{
    public sealed class CookedPostFormViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn công thức gốc.")]
        public int RecipeId { get; set; }
        [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự.")]
        public string Note { get; set; } = string.Empty;
        public IFormFile? ImageFile { get; set; }
        public Guid SubmissionKey { get; set; } = Guid.NewGuid();
        [BindNever]
        public string RecipeName { get; set; } = string.Empty;
        [BindNever]
        public string? ExistingImageUrl { get; set; }
        [BindNever]
        public int? PostId { get; set; }
        public string Query { get; set; } = string.Empty;
        [BindNever]
        public IReadOnlyList<CookedPostRecipeChoice> Recipes { get; set; } = Array.Empty<CookedPostRecipeChoice>();
    }

    public sealed record CookedPostRecipeChoice(int Id, string Name, string? AuthorName);

    public sealed class CookedPostViewModel
    {
        public int Id { get; set; }
        public int RecipeId { get; set; }
        public string RecipeName { get; set; } = string.Empty;
        public string AuthorId { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Note { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsOwner { get; set; }

        public static string MediaUrl(int id, string fileName) => $"/media/cooked-posts/{id}/{fileName}";
    }
}
