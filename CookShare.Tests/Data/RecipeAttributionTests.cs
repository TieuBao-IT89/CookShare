using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Tests.Data;

public sealed class RecipeAttributionTests
{
    [Fact]
    public async Task DiscoveryAndMixedFeed_ShouldRetainSourceAndImporterWithoutInventingEngagement()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(new ApplicationUser { Id = "importer", UserName = "cook", DisplayName = "Người nhập" });
        var imported = new Recipe
        {
            Name = "Imported meal",
            UserId = "importer",
            Source = "themealdb",
            ExternalId = "12345",
            SourceUrl = "https://www.themealdb.com/meal.php?c=12345",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Recipes.Add(imported);
        await db.SaveChangesAsync();
        db.CookedRecipePosts.Add(new CookedRecipePost
        {
            RecipeId = imported.Id,
            UserId = "importer",
            Note = "My own finished dish",
            ImageFileName = "dish.png",
            SubmissionKey = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var discovery = await SocialDiscoveryQueries.LoadAsync(db, new DiscoveryOptions("", "", "", "", null, null, "newest"), null);
        var card = Assert.Single(discovery.Recipes);
        Assert.Equal("themealdb", card.Source);
        Assert.Equal("12345", card.ExternalId);
        Assert.Equal(imported.SourceUrl, card.SourceUrl);
        Assert.Equal("Người nhập", card.User!.DisplayName);

        var feed = await RecipeFeedQueries.LoadAsync(db, null, "latest", null);
        var recipePost = Assert.Single(feed.Recipes.Where(post => post.Kind == 0));
        Assert.Equal(card.Source, recipePost.Source);
        Assert.Equal(card.ExternalId, recipePost.ExternalId);
        Assert.Equal("importer", recipePost.AuthorId);
        Assert.Equal(0, recipePost.LikeCount);
        Assert.Equal(0, recipePost.CommentCount);
        Assert.Equal(0, recipePost.RatingCount);
        var ownPhoto = Assert.Single(feed.Recipes.Where(post => post.Kind == 1));
        Assert.Null(ownPhoto.Source);
        Assert.Null(ownPhoto.ExternalId);
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
