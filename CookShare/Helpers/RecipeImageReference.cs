namespace CookShare.Helpers
{
    public static class RecipeImageReference
    {
        public const string Placeholder = "/img/recipe-placeholder.svg";
        public const int MaxLength = 2000;
        private static readonly char[] _pathSeparators = { '?', '#' };

        public static bool IsValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            string reference = value.Trim();
            if (reference.Length > MaxLength || reference.Any(char.IsControl) || reference.Contains('\\'))
            {
                return false;
            }

            if (reference.StartsWith("~/", StringComparison.Ordinal))
            {
                reference = reference[1..];
            }

            if (reference.StartsWith('/'))
            {
                if (reference.StartsWith("//", StringComparison.Ordinal))
                {
                    return false;
                }

                string path = reference.Split(_pathSeparators, 2)[0];
                string decodedPath = Uri.UnescapeDataString(path);
                return !decodedPath.Contains('\\')
                    && !decodedPath.Any(char.IsControl)
                    && !decodedPath.StartsWith("//", StringComparison.Ordinal)
                    && !decodedPath.Split('/').Any(segment => segment is "." or "..");
            }

            return Uri.TryCreate(reference, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                && !string.IsNullOrEmpty(uri.Host)
                && string.IsNullOrEmpty(uri.UserInfo);
        }

        public static string GetSource(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || !IsValid(value))
            {
                return Placeholder;
            }

            string reference = value.Trim();
            return reference.StartsWith("~/", StringComparison.Ordinal) ? reference[1..] : reference;
        }
    }
}
