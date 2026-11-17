using CookShare.Helpers;
using CookShare.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CookShare.Data.Seeding
{
    public sealed class RecipeSeeder(
        ApplicationDbContext db,
        IHostEnvironment environment,
        ILookupNormalizer normalizer,
        IEnumerable<IRecipeSeedSource> sources)
    {
        public async Task<RecipeSeedResult> RunAsync(RecipeSeedOptions options, CancellationToken cancellationToken = default)
        {
            // Enforced in the service too: callers cannot bypass the CLI environment guard.
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException("Recipe seeding is allowed only in Development or Testing.");
            }

            options.Validate();
            var result = new RecipeSeedResult();
            var names = options.UserNames.Select(name => normalizer.NormalizeName(name)!).ToArray();
            var users = await db.Users.AsNoTracking()
                .Where(user => names.Contains(user.NormalizedUserName!))
                .Select(user => new { user.Id, user.NormalizedUserName })
                .ToDictionaryAsync(user => user.NormalizedUserName!, user => user.Id, cancellationToken);
            for (int index = 0; index < names.Length; index++)
            {
                if (!users.ContainsKey(names[index]))
                {
                    result.Errors.Add($"Identity user '{options.UserNames[index]}' does not exist. Nothing was imported.");
                }
            }

            if (result.Errors.Count > 0)
            {
                result.Failed = options.Count;
                return result;
            }

            var source = sources.Single(candidate => candidate.Name == options.Source);
            var batch = await source.ReadAsync(options, cancellationToken);
            result.Errors.AddRange(batch.Errors);
            result.Failed = batch.Errors.Count;
            var records = batch.Recipes.Take(options.Count - Math.Min(options.Count, result.Failed)).ToList();
            var keys = records.Select(record => record.Key).ToArray();
            var existingKeys = await db.Recipes.AsNoTracking()
                .Where(recipe => recipe.Source == options.Source && keys.Contains(recipe.ExternalId!))
                .Select(recipe => recipe.ExternalId!)
                .ToListAsync(cancellationToken);
            var seen = new HashSet<string>(existingKeys, StringComparer.Ordinal);
            var pending = new List<Recipe>();
            DateTime now = DateTime.UtcNow;
            for (int index = 0; index < records.Count; index++)
            {
                var record = records[index];
                var errors = record.Validate();
                if (errors.Count > 0)
                {
                    result.Failed++;
                    result.Errors.Add($"Recipe '{record.Key}': {string.Join("; ", errors)}");
                    continue;
                }

                if (!seen.Add(record.Key))
                {
                    result.Skipped++;
                    continue;
                }

                int ownerIndex = index % names.Length;
                pending.Add(new Recipe
                {
                    Name = record.Name.Trim(),
                    Description = record.Description.Trim(),
                    Image = RecipeImageReference.GetSource(record.Image),
                    Ingredients = record.Ingredients.ReplaceLineEndings("\n").Trim(),
                    Instructions = record.Instructions.ReplaceLineEndings("\n").Trim(),
                    Category = record.Category,
                    UserId = users[names[ownerIndex]],
                    CreatedAt = now.AddMinutes(index - records.Count),
                    Source = options.Source,
                    ExternalId = record.Key,
                    SourceUrl = record.SourceUrl
                });
                result.Preview.Add(new RecipeSeedPreview(options.UserNames[ownerIndex], record));
            }

            if (records.Count == 0 && result.Failed == 0)
            {
                result.Errors.Add("The source returned no recipes. Nothing was imported.");
                result.Failed = 1;
            }

            if (options.DryRun)
            {
                result.WouldAdd = pending.Count;
                return result;
            }

            if (pending.Count > 0)
            {
                db.Recipes.AddRange(pending);
                try
                {
                    // One SaveChanges uses EF Core's relational transaction: the entire batch commits or rolls back.
                    // The unique index also rejects concurrent imports of the same source key.
                    await db.SaveChangesAsync(cancellationToken);
                    result.Added = pending.Count;
                }
                catch (DbUpdateException exception) when (exception.InnerException is PostgresException)
                {
                    foreach (var recipe in pending)
                    {
                        db.Entry(recipe).State = EntityState.Detached;
                    }

                    result.Failed += pending.Count;
                    result.Errors.Add("The database rejected the batch; no recipes from this batch were added. Check migrations and users, or rerun after a concurrent import.");
                }
            }

            return result;
        }
    }
}
