
using System.ComponentModel.DataAnnotations;
using CookShare.Helpers;

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
        [ProfileImage]
        public string? AvatarUrl { get; set; }

        [StringLength(ProfileContent.ImageUrlMaxLength, ErrorMessage = "Đường dẫn ảnh bìa không được quá 500 ký tự.")]
        [ProfileImage]
        public string? CoverImageUrl { get; set; }

        private string? _bio;

        [StringLength(ProfileContent.BioMaxLength, ErrorMessage = "Tiểu sử không được quá 300 ký tự.")]
        [PlainBio]
        public string? Bio
        {
            get => _bio;
            set => _bio = value?.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        }
    }
}
