using System.ComponentModel.DataAnnotations;
using CookShare.Helpers;
using CookShare.ViewModels;

namespace CookShare.Data.Seeding
{
    public sealed record RecipeSeedRecord
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string? Image { get; init; }
        public string Ingredients { get; init; } = string.Empty;
        public string Instructions { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string? SourceUrl { get; init; }

        public static IReadOnlySet<string> Categories { get; } = new HashSet<string>(StringComparer.Ordinal)
        {
            "Món Việt", "Món Á", "Món Âu", "Món chính", "Món phụ", "Món chay", "Món tráng miệng", "Đồ uống", "Ăn sáng"
        };

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<ValidationResult>();
            var form = new RecipeFormViewModel
            {
                Name = Name,
                Description = Description,
                Image = Image,
                Ingredients = Ingredients,
                Instructions = Instructions,
                Category = Category
            };
            Validator.TryValidateObject(form, new ValidationContext(form), errors, validateAllProperties: true);
            var messages = errors.Select(error => error.ErrorMessage ?? "Invalid recipe field.").ToList();
            if (string.IsNullOrWhiteSpace(Key) || Key.Length > 128 || Key != Key.Trim())
            {
                messages.Add("A stable source key of 1 to 128 characters is required.");
            }

            if (!Categories.Contains(Category))
            {
                messages.Add("Category is not supported by CookShare.");
            }

            if (SourceUrl != null && (!Uri.TryCreate(SourceUrl, UriKind.Absolute, out _)
                || !RecipeImageReference.IsValid(SourceUrl)))
            {
                messages.Add("SourceUrl must be an absolute HTTP(S) URL without credentials.");
            }

            return messages;
        }
    }

    public sealed record RecipeSeedBatch(IReadOnlyList<RecipeSeedRecord> Recipes, IReadOnlyList<string> Errors);

    public interface IRecipeSeedSource
    {
        string Name { get; }
        Task<RecipeSeedBatch> ReadAsync(RecipeSeedOptions options, CancellationToken cancellationToken = default);
    }
}
