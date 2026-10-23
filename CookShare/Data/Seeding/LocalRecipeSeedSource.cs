using System.Text.Json;

namespace CookShare.Data.Seeding
{
    public sealed class LocalRecipeSeedSource(IHostEnvironment environment) : IRecipeSeedSource
    {
        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public string Name => "local";

        public async Task<RecipeSeedBatch> ReadAsync(RecipeSeedOptions options, CancellationToken cancellationToken = default)
        {
            string path = Path.Combine(environment.ContentRootPath, "Data", "Seeds", "sample-recipes.json");
            await using var stream = File.OpenRead(path);
            var records = await JsonSerializer.DeserializeAsync<List<RecipeSeedRecord?>>(
                stream, _jsonOptions, cancellationToken) ?? throw new JsonException("Expected a recipe array.");
            var selected = records.Take(options.Count).ToArray();
            var errors = selected.Where(record => record == null).Select(_ => "Local recipe entry is null.").ToList();
            return new RecipeSeedBatch(selected.OfType<RecipeSeedRecord>().ToList(), errors);
        }
    }
}
