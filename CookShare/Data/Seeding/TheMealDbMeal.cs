using System.Text.Json;
using System.Text.Json.Serialization;

namespace CookShare.Data.Seeding
{
    public sealed class TheMealDbMeal
    {
        [JsonPropertyName("idMeal")]
        public string? Id { get; set; }

        [JsonPropertyName("strMeal")]
        public string? Name { get; set; }

        [JsonPropertyName("strMealThumb")]
        public string? Image { get; set; }

        [JsonPropertyName("strCategory")]
        public string? Category { get; set; }

        [JsonPropertyName("strArea")]
        public string? Area { get; set; }

        [JsonPropertyName("strInstructions")]
        public string? Instructions { get; set; }

        [JsonPropertyName("strSource")]
        public string? SourceUrl { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> Fields { get; set; } = new(StringComparer.Ordinal);
    }
}
