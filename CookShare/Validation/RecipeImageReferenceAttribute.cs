using System.ComponentModel.DataAnnotations;
using CookShare.Helpers;

namespace CookShare.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class RecipeImageReferenceAttribute : ValidationAttribute
    {
        public RecipeImageReferenceAttribute()
        {
            ErrorMessage = "Vui lòng dùng URL ảnh http/https hoặc đường dẫn ảnh bắt đầu bằng /. Không hỗ trợ ảnh Base64.";
        }

        public override bool IsValid(object? value)
        {
            return value is null || value is string reference && RecipeImageReference.IsValid(reference);
        }
    }
}
