using System.ComponentModel.DataAnnotations;
using CookShare.Helpers;
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

        [Range(1, 20, ErrorMessage = "Khẩu phần phải từ 1 đến 20.")]
        [Display(Name = "Khẩu phần")]
        public int Servings { get; set; } = 4;

        [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;

        [StringLength(RecipeImageReference.MaxLength)]
        [RecipeImageReferenceAttribute]
        public string? Image { get; set; }
    }
}
