using System.Globalization;
using System.Text;

namespace CookShare.Helpers
{
    public static class VietnameseSearch
    {
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var result = new StringBuilder(value.Length);
            foreach (char character in value.Trim().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    result.Append(character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character));
                }
            }

            return result.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
