using System.ComponentModel.DataAnnotations;

namespace CookShare.Models
{
    public class Recipe
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên món ăn.")]
        [Display(Name = "Tên món ăn")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mô tả.")]
        [Display(Name = "Mô tả")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Ảnh món ăn")]
        public string? Image { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nguyên liệu.")]
        [Display(Name = "Nguyên liệu")]
        public string Ingredients { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập hướng dẫn chế biến.")]
        [Display(Name = "Hướng dẫn chế biến")]
        public string Instructions { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
        [Display(Name = "Danh mục")]
        public string Category { get; set; } = string.Empty;
    }
}