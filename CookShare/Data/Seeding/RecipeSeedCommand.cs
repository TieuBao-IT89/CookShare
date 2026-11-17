using System.Data.Common;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Data.Seeding
{
    public static class RecipeSeedCommand
    {
        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        public const string Help = """
            Usage: seed-recipes [--source local|themealdb] [--count 1..100]
                                [--users admintest1,root123] [--dry-run]
                                [--search name | --meal-id id]
            Defaults: local, 30 recipes, admintest1 and root123 (round-robin).
            Development/Testing only. Dry run reads users and existing keys but writes nothing.
            --search and --meal-id apply only to TheMealDB; otherwise it searches letters a-z.
            Count limits source candidates; existing and invalid candidates are not replaced.
            See docs/RECIPE_SEEDING.md for database setup, attribution, and examples.
            """;

        public static async Task<int> RunAsync(
            string[] args, IServiceProvider services, TextWriter output, CancellationToken cancellationToken = default)
        {
            if (args is ["--help"])
            {
                await output.WriteLineAsync(Help);
                return 0;
            }

            RecipeSeedOptions options;
            try
            {
                options = RecipeSeedOptions.Parse(args);
            }
            catch (ArgumentException exception)
            {
                await output.WriteLineAsync(exception.Message);
                return 1;
            }

            try
            {
                using var scope = services.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<RecipeSeeder>().RunAsync(options, cancellationToken);
                if (options.DryRun)
                {
                    await output.WriteLineAsync(JsonSerializer.Serialize(result.Preview, _jsonOptions));
                }

                foreach (string error in result.Errors)
                {
                    await output.WriteLineAsync(error);
                }

                await output.WriteLineAsync($"Added: {result.Added}; skipped: {result.Skipped}; failed: {result.Failed}; would add: {result.WouldAdd}.");
                return result.Failed == 0 ? 0 : 1;
            }
            catch (Exception exception) when (exception is DbException or DbUpdateException)
            {
                await output.WriteLineAsync("Database access failed. Check the Development/Testing connection, existing users, and applied migrations. No successful import was reported.");
            }
            catch (HttpRequestException)
            {
                await output.WriteLineAsync("TheMealDB request failed. Nothing was imported; try again later.");
            }
            catch (OperationCanceledException)
            {
                await output.WriteLineAsync("Seeding was cancelled or timed out. No successful import was reported; rerun to check existing keys before retrying.");
            }
            catch (JsonException)
            {
                await output.WriteLineAsync("The recipe source returned invalid JSON. Nothing was imported.");
            }
            catch (IOException)
            {
                await output.WriteLineAsync("Could not read the recipe source. Nothing was imported; check that sample-recipes.json is included in the build.");
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                await output.WriteLineAsync(exception.Message);
            }

            return 1;
        }
    }
}
