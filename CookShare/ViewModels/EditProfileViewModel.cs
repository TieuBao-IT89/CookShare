
using System.ComponentModel.DataAnnotations;

namespace CookShare.ViewModels
{
    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên hiển thị.")]
        [StringLength(
            100,
            MinimumLength = 2,
            ErrorMessage = "Tên hiển thị phải từ 2 đến 100 ký tự.")]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(
            500,
            ErrorMessage = "Đường dẫn ảnh không được quá 500 ký tự.")]
        public string? AvatarUrl { get; set; }
    }
}
