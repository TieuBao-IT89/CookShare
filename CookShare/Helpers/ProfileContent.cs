using System.ComponentModel.DataAnnotations;

namespace CookShare.Helpers
{
    public static class ProfileContent
    {
        public const int ImageUrlMaxLength = 500;
        public const int BioMaxLength = 300;
        public const string DefaultAvatar = "/img/recipes/profile-avatar.webp";

        public static bool IsValidImage(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            if (value.Trim().Length > ImageUrlMaxLength || !RecipeImageReference.IsValid(value)) return false;
            string source = RecipeImageReference.GetSource(value);
            return source.StartsWith('/') || Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
        }

        public static string? ImageSource(string? value)
        {
            return string.IsNullOrWhiteSpace(value) || !IsValidImage(value) ? null : RecipeImageReference.GetSource(value);
        }

        public static bool IsPlainBio(string? value)
        {
            if (value == null) return true;
            if (value.Any(character => char.IsControl(character) && character is not '\n' and not '\r' and not '\t')) return false;
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] != '<') continue;
                int next = index + 1;
                while (next < value.Length && char.IsWhiteSpace(value[next])) next++;
                if (next < value.Length && (char.IsLetter(value[next]) || value[next] is '/' or '!' or '?')) return false;
            }

            return true;
        }
    }

    public sealed class ProfileImageAttribute : ValidationAttribute
    {
        public ProfileImageAttribute() : base("Ảnh phải là đường dẫn nội bộ hoặc URL HTTPS hợp lệ, tối đa 500 ký tự.") { }
        public override bool IsValid(object? value) => value == null || value is string text && ProfileContent.IsValidImage(text);
    }

    public sealed class PlainBioAttribute : ValidationAttribute
    {
        public PlainBioAttribute() : base("Tiểu sử chỉ hỗ trợ văn bản thuần, không chứa thẻ HTML.") { }
        public override bool IsValid(object? value) => value == null || value is string text && ProfileContent.IsPlainBio(text);
    }
}
