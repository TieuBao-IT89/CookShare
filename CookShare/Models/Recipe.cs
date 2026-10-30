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
        [Display(Name = "Danh mục")]
        public string Category { get; set; } = string.Empty;

        public string? UserId { get; set; }

        public CookShare.Models.Identity.ApplicationUser? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Null for recipes written by users. The pair identifies an imported recipe.
        public string? Source { get; set; }

        public string? ExternalId { get; set; }

        public string? SourceUrl { get; set; }

        public ICollection<RecipeLike> Likes { get; set; } = new List<RecipeLike>();

        public ICollection<RecipeComment> Comments { get; set; } = new List<RecipeComment>();

        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

        public ICollection<RecipeRating> Ratings { get; set; } = new List<RecipeRating>();
    }
}
