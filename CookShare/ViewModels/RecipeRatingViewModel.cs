using System.ComponentModel.DataAnnotations;

namespace CookShare.ViewModels
{
    public sealed class RecipeRatingFormViewModel
    {
        [Range(1, 5, ErrorMessage = "Vui lòng chọn số sao từ 1 đến 5.")]
        public int Stars { get; set; }
    }

    public sealed class RecipeRatingViewModel
    {
        public int RecipeId { get; set; }
        public int RatingCount { get; set; }
        public double AverageRating { get; set; }
        public int? UserRating { get; set; }
        public bool CanRate { get; set; }
        public string? ViewerId { get; set; }
    }
}
