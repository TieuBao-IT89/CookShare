using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CookShare.Tests.Integration
{
    [Collection(PostgreSqlIntegrationCollection.Name)]
    public sealed class FollowingFeedPostgresTests
    {
        [PostgresFact]
        public async Task Following_ShouldPersistFilterAndPageCorrectlyAcrossPostgresConnections()
        {
            string connection = Environment.GetEnvironmentVariable("COOKSHARE_TEST_CONNECTION")!;
            string? database = new NpgsqlConnectionStringBuilder(connection).Database;
            Assert.True(database == "cookshare_test" || database?.StartsWith("cookshare_test_", StringComparison.Ordinal) == true);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
            string suffix = Guid.NewGuid().ToString("N");
            string reader = "feed-reader-" + suffix;
            string author = "feed-author-" + suffix;
            int[] expected = Array.Empty<int>();
            try
            {
                await using (var seed = new ApplicationDbContext(options))
                {
                    seed.Users.AddRange(new ApplicationUser { Id = reader, UserName = reader }, new ApplicationUser { Id = author, UserName = author });
                    seed.Follows.Add(new Follow { FollowerId = reader, FollowingId = author });
                    var timestamp = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
                    for (int index = 0; index < 19; index++)
                    {
                        seed.Recipes.Add(new Recipe
                        {
                            Name = $"Feed database recipe {index}",
                            Description = "Test recipe",
                            Category = "Món Việt",
                            Ingredients = "Rice",
                            Instructions = "Cook",
                            UserId = author,
                            CreatedAt = timestamp
                        });
                    }

                    await seed.SaveChangesAsync();
                    expected = await seed.Recipes.Where(recipe => recipe.UserId == author).OrderByDescending(recipe => recipe.Id)
                        .Select(recipe => recipe.Id).ToArrayAsync();
                    seed.RecipeLikes.Add(new RecipeLike { RecipeId = expected[0], UserId = reader });
                    seed.Favorites.Add(new Favorite { RecipeId = expected[0], UserId = reader });
                    await seed.SaveChangesAsync();
                }

                var actual = new List<int>();
                RecipeFeedCursor? cursor = null;
                do
                {
                    await using var read = new ApplicationDbContext(options);
                    var page = await RecipeFeedQueries.LoadAsync(read, reader, "following", cursor);
                    Assert.All(page.Recipes, recipe => Assert.Equal(author, recipe.AuthorId));
                    if (cursor == null)
                    {
                        Assert.True(page.Recipes[0].IsLiked);
                        Assert.True(page.Recipes[0].IsSaved);
                    }

                    actual.AddRange(page.Recipes.Select(recipe => recipe.Id));
                    Assert.True(RecipeFeedCursor.TryParse(page.NextCursor, out cursor));
                } while (cursor != null);

                Assert.Equal(expected, actual);
                await using var unfollow = new ApplicationDbContext(options);
                await unfollow.Follows.Where(follow => follow.FollowerId == reader).ExecuteDeleteAsync();
                Assert.Empty((await RecipeFeedQueries.LoadAsync(unfollow, reader, "following", null)).Recipes);
            }
            finally
            {
                await using var cleanup = new ApplicationDbContext(options);
                await cleanup.RecipeLikes.Where(like => like.UserId == reader).ExecuteDeleteAsync();
                await cleanup.Favorites.Where(favorite => favorite.UserId == reader).ExecuteDeleteAsync();
                await cleanup.Follows.Where(follow => follow.FollowerId == reader).ExecuteDeleteAsync();
                await cleanup.Recipes.Where(recipe => recipe.UserId == author).ExecuteDeleteAsync();
                await cleanup.Users.Where(user => user.Id == reader || user.Id == author).ExecuteDeleteAsync();
            }
        }
    }
}
