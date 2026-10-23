using System.ComponentModel.DataAnnotations;
using CookShare.Helpers;

namespace CookShare.Data.Seeding
{
    public static class TheMealDbMapper
    {
        public static RecipeSeedRecord Map(TheMealDbMeal meal)
        {
            ArgumentNullException.ThrowIfNull(meal);
            string key = meal.Id?.Trim() ?? string.Empty;
            if (key.Length is < 1 or > 128 || !key.All(char.IsAsciiDigit))
            {
                throw new ValidationException("Missing or invalid TheMealDB idMeal.");
            }

            var ingredients = new List<string>();
            for (int index = 1; index <= 20; index++)
            {
                string ingredient = Field(meal, $"strIngredient{index}");
                if (string.IsNullOrWhiteSpace(ingredient))
                {
                    continue;
                }

                string measure = Field(meal, $"strMeasure{index}");
                ingredients.Add(string.IsNullOrWhiteSpace(measure) ? ingredient : $"{measure} {ingredient}");
            }

            string name = meal.Name?.Trim() ?? string.Empty;
            string category = MapCategory(meal.Category?.Trim());
            string? sourceUrl = meal.SourceUrl?.Trim();
            if (string.IsNullOrWhiteSpace(sourceUrl) || !Uri.TryCreate(sourceUrl, UriKind.Absolute, out _)
                || !RecipeImageReference.IsValid(sourceUrl))
            {
                sourceUrl = $"https://www.themealdb.com/meal.php?c={key}";
            }

            string description = $"{name} — {meal.Category?.Trim()}";
            if (!string.IsNullOrWhiteSpace(meal.Area))
            {
                description += $" ({meal.Area.Trim()})";
            }

            return new RecipeSeedRecord
            {
                Key = key,
                Name = name,
                Description = description,
                Image = RecipeImageReference.GetSource(meal.Image),
                Ingredients = string.Join('\n', ingredients),
                Instructions = (meal.Instructions ?? string.Empty).ReplaceLineEndings("\n").Trim(),
                Category = category,
                SourceUrl = sourceUrl
            };
        }

        private static string Field(TheMealDbMeal meal, string name)
        {
            return meal.Fields.TryGetValue(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
                ? value.GetString()?.Trim() ?? string.Empty
                : string.Empty;
        }

        private static string MapCategory(string? category)
        {
            return category?.ToUpperInvariant() switch
            {
                "DESSERT" => "Món tráng miệng",
                "STARTER" or "SIDE" => "Món phụ",
                "VEGAN" or "VEGETARIAN" => "Món chay",
                "BREAKFAST" => "Ăn sáng",
                "BEEF" or "CHICKEN" or "GOAT" or "LAMB" or "MISCELLANEOUS" or "PASTA"
                    or "PORK" or "SEAFOOD" => "Món chính",
                _ => throw new ValidationException("Missing or unsupported TheMealDB category; add an explicit mapping before importing.")
            };
        }
    }
}
