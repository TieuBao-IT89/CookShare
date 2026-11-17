using System.Globalization;

namespace CookShare.Data.Seeding
{
    public sealed record RecipeSeedOptions
    {
        public string Source { get; init; } = "local";
        public int Count { get; init; } = 30;
        public IReadOnlyList<string> UserNames { get; init; } = new[] { "admintest1", "root123" };
        public bool DryRun { get; init; }
        public string? Search { get; init; }
        public string? MealId { get; init; }

        public static RecipeSeedOptions Parse(string[] args)
        {
            var options = new RecipeSeedOptions();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < args.Length; index++)
            {
                string name = args[index];
                if (!seen.Add(name))
                {
                    throw new ArgumentException($"Duplicate option: {name}");
                }

                if (name == "--dry-run")
                {
                    options = options with { DryRun = true };
                    continue;
                }

                if (name is not ("--source" or "--count" or "--users" or "--search" or "--meal-id"))
                {
                    throw new ArgumentException($"Unknown option: {name}. Use seed-recipes --help.");
                }

                if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Missing value for {name}.");
                }

                string value = args[index].Trim();
                options = name switch
                {
                    "--source" => options with { Source = value.ToLowerInvariant() },
                    "--count" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int count)
                        => options with { Count = count },
                    "--count" => throw new ArgumentException("--count must be an integer between 1 and 100."),
                    "--users" => options with { UserNames = value.Split(',', StringSplitOptions.TrimEntries) },
                    "--search" => options with { Search = value },
                    _ => options with { MealId = value }
                };
            }

            options.Validate();
            return options;
        }

        public void Validate()
        {
            if (Source is not ("local" or "themealdb"))
            {
                throw new ArgumentException("--source must be local or themealdb.");
            }

            if (Count is < 1 or > 100)
            {
                throw new ArgumentException("--count must be between 1 and 100.");
            }

            if (UserNames.Count == 0 || UserNames.Any(name => string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Length > 256)
                || UserNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != UserNames.Count)
            {
                throw new ArgumentException("--users must contain distinct, non-empty Identity usernames separated by commas.");
            }

            if ((Search != null || MealId != null) && Source != "themealdb")
            {
                throw new ArgumentException("--search and --meal-id require --source themealdb.");
            }

            if (Search != null && (string.IsNullOrWhiteSpace(Search) || Search.Length > 200))
            {
                throw new ArgumentException("--search must contain 1 to 200 characters.");
            }

            if (MealId != null && (MealId.Length is < 1 or > 128 || !MealId.All(char.IsAsciiDigit)))
            {
                throw new ArgumentException("--meal-id must be a numeric TheMealDB identifier.");
            }

            if (Search != null && MealId != null)
            {
                throw new ArgumentException("Choose either --search or --meal-id.");
            }
        }
    }
}
