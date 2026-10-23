using System.ComponentModel.DataAnnotations;

namespace CookShare.ViewModels
{
    public class CommentFormViewModel
    {
        // Preserve the original composer limit within the existing 1000-character database column.
        public const int MaxLength = 500;
        private string _content = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập nội dung bình luận.")]
        [StringLength(MaxLength, ErrorMessage = "Bình luận không được vượt quá 500 ký tự.")]
        public string Content
        {
            get => _content;
            set => _content = value?.Trim() ?? string.Empty;
        }
    }
}
