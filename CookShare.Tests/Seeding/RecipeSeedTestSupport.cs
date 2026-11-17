using CookShare.Data;
using CookShare.Data.Seeding;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CookShare.Tests.Seeding
{
    internal static class RecipeSeedTestSupport
    {
        public static ApplicationDbContext CreateDb()
        {
            return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("seeding-" + Guid.NewGuid()).Options);
        }

        public static async Task AddUsersAsync(ApplicationDbContext db)
        {
            foreach (string name in new[] { "admintest1", "root123" })
            {
                db.Users.Add(new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = name,
                    NormalizedUserName = new UpperInvariantLookupNormalizer().NormalizeName(name)
                });
            }

            await db.SaveChangesAsync();
        }

        public static RecipeSeeder Seeder(ApplicationDbContext db, IRecipeSeedSource source, string environment = "Testing")
        {
            return new RecipeSeeder(db, new SeedEnvironment { EnvironmentName = environment },
                new UpperInvariantLookupNormalizer(), new[] { source });
        }

        public static RecipeSeedRecord Record(string key = "test-recipe") => new()
        {
            Key = key,
            Name = "Test recipe",
            Description = "Original sample recipe",
            Ingredients = "100 g mushrooms\n1 tsp oil",
            Instructions = "Slice the mushrooms.\nCook in oil until tender.",
            Category = "Món chay"
        };
    }

    internal sealed class SeedEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "CookShare";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    internal sealed class StubRecipeSeedSource(params RecipeSeedRecord[] records) : IRecipeSeedSource
    {
        public string Name => "local";
        public int Calls { get; private set; }
        public Exception? Failure { get; init; }

        public Task<RecipeSeedBatch> ReadAsync(RecipeSeedOptions options, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Failure != null)
            {
                return Task.FromException<RecipeSeedBatch>(Failure);
            }

            return Task.FromResult(new RecipeSeedBatch(records, Array.Empty<string>()));
        }
    }
}
