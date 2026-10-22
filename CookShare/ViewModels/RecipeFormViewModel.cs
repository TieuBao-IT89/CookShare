
using System.ComponentModel.DataAnnotations;

namespace CookShare.ViewModels
{
    public class RecipeFormViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên món ăn.")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mô tả.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập nguyên liệu.")]
        public string Ingredients { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập cách chế biến.")]
        public string Instructions { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Image { get; set; }
    }
}
