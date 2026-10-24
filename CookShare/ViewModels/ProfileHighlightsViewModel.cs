using System.ComponentModel.DataAnnotations;
using CookShare.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CookShare.ViewModels
{
    public sealed class ProfileHighlightsViewModel
    {
        [MaxLength(3, ErrorMessage = "Bạn chỉ có thể chọn tối đa 3 công thức nổi bật.")]
        public List<int> RecipeIds { get; set; } = new();
        [BindNever]
        public List<Recipe> AvailableRecipes { get; set; } = new();
    }
}
