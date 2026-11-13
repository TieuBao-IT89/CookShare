using System.ComponentModel.DataAnnotations;
using CookShare.Models;

namespace CookShare.ViewModels
{
    public sealed class CollectionInputViewModel
    {
        [Required(ErrorMessage = "Hãy đặt tên cho bộ sưu tập.")]
        [StringLength(80, ErrorMessage = "Tên bộ sưu tập tối đa 80 ký tự.")]
        [Display(Name = "Tên bộ sưu tập")]
        public string Name { get; set; } = string.Empty;

        [StringLength(300, ErrorMessage = "Mô tả tối đa 300 ký tự.")]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Cho phép mọi người xem bộ sưu tập này")]
        public bool IsPublic { get; set; }

        public int? RecipeId { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public sealed class RecipeCollectionSummaryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsPublic { get; set; }
        public int RecipeCount { get; set; }
        public string? CoverImage { get; set; }
        public bool ContainsRecipe { get; set; }
    }

    public sealed class CollectionIndexViewModel
    {
        public IReadOnlyList<RecipeCollectionSummaryViewModel> Collections { get; set; } = Array.Empty<RecipeCollectionSummaryViewModel>();
        public int Page { get; set; } = 1;
        public bool HasMore { get; set; }
    }

    public sealed class CollectionDetailsViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public bool IsOwner { get; set; }
        public int RecipeCount { get; set; }
        public IReadOnlyList<Recipe> Recipes { get; set; } = Array.Empty<Recipe>();
        public int Page { get; set; } = 1;
        public bool HasMore { get; set; }
    }

    public sealed class CollectionChooserViewModel
    {
        public int RecipeId { get; set; }
        public string RecipeName { get; set; } = string.Empty;
        public string? RecipeImage { get; set; }
        public bool IsSaved { get; set; }
        public IReadOnlyList<RecipeCollectionSummaryViewModel> Collections { get; set; } = Array.Empty<RecipeCollectionSummaryViewModel>();
        public CollectionInputViewModel NewCollection { get; set; } = new();
        public string? ReturnUrl { get; set; }
        public int Page { get; set; } = 1;
        public bool HasMore { get; set; }
    }
}
