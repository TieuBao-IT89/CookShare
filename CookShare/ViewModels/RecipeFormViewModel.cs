using System.ComponentModel.DataAnnotations;
using CookShare.Helpers;
using CookShare.Models;
using CookShare.Validation;

namespace CookShare.ViewModels
{
    public class RecipeFormViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên món ăn.")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mô tả.")]
        [StringLength(2000, ErrorMessage = "Mô tả không được vượt quá 2000 ký tự.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập nguyên liệu.")]
        [StringLength(10000, ErrorMessage = "Nguyên liệu không được vượt quá 10000 ký tự.")]
        public string Ingredients { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập cách chế biến.")]
        [StringLength(20000, ErrorMessage = "Cách chế biến không được vượt quá 20000 ký tự.")]
        public string Instructions { get; set; } = string.Empty;


        [Range(1, 1440, ErrorMessage = "Thời gian chuẩn bị phải từ 1 đến 1440 phút.")]
        [Display(Name = "Thời gian chuẩn bị (phút)")]
        public int? PreparationTimeMinutes { get; set; }

        [Range(1, 1440, ErrorMessage = "Thời gian nấu phải từ 1 đến 1440 phút.")]
        [Display(Name = "Thời gian nấu (phút)")]
        public int? CookingTimeMinutes { get; set; }

        [EnumDataType(typeof(RecipeDifficulty), ErrorMessage = "Vui lòng chọn độ khó hợp lệ.")]
        [Display(Name = "Độ khó")]
        public RecipeDifficulty? Difficulty { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;

        [StringLength(RecipeImageReference.MaxLength)]
        [RecipeImageReferenceAttribute]
        public string? Image { get; set; }
    }
}
