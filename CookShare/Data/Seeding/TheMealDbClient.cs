using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CookShare.Data.Seeding
{
    public sealed class TheMealDbClient(HttpClient httpClient, IConfiguration configuration) : IRecipeSeedSource
    {
        public string Name => "themealdb";

        public async Task<RecipeSeedBatch> ReadAsync(RecipeSeedOptions options, CancellationToken cancellationToken = default)
        {
            string apiKey = configuration["TheMealDb:ApiKey"] ?? "1";
            if (apiKey.Length is < 1 or > 128 || !apiKey.All(char.IsAsciiLetterOrDigit))
            {
                throw new ArgumentException("TheMealDb:ApiKey must be an alphanumeric API key.");
            }

            // V1 only. At most 26 requests, with deterministic discovery instead of random retries.
            IEnumerable<string> queries = options.MealId != null
                ? new[] { "lookup.php?i=" + Uri.EscapeDataString(options.MealId) }
                : options.Search != null
                    ? new[] { "search.php?s=" + Uri.EscapeDataString(options.Search) }
                    : Enumerable.Range('a', 26).Select(letter => "search.php?f=" + (char)letter);
            var records = new List<RecipeSeedRecord>();
            var errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string query in queries)
            {
                var address = new Uri($"https://www.themealdb.com/api/json/v1/{apiKey}/{query}");
                using var response = await httpClient.GetAsync(address, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("meals", out var meals)
                    || meals.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null))
                {
                    throw new JsonException("Expected a TheMealDB meals array or null.");
                }

                if (meals.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                foreach (var item in meals.EnumerateArray())
                {
                    try
                    {
                        var meal = item.Deserialize<TheMealDbMeal>() ?? throw new JsonException("Null meal.");
                        var record = TheMealDbMapper.Map(meal);
                        if (seen.Add(record.Key))
                        {
                            records.Add(record);
                        }
                    }
                    catch (Exception exception) when (exception is JsonException or ValidationException)
                    {
                        errors.Add("Invalid TheMealDB meal: " + exception.Message);
                    }

                    if (records.Count + errors.Count >= options.Count)
                    {
                        return new RecipeSeedBatch(records, errors);
                    }
                }
            }

            return new RecipeSeedBatch(records, errors);
        }
    }
}
