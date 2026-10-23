using System.ComponentModel.DataAnnotations;
using CookShare.Data;
using CookShare.Models;
using CookShare.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Tests.Data
{
    public sealed class RecipeRatingQueriesTests
    {
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(5, true)]
        [InlineData(6, false)]
        public void RatingForm_ShouldValidateRange(int stars, bool valid)
        {
            var input = new RecipeRatingFormViewModel { Stars = stars };
            Assert.Equal(valid, Validator.TryValidateObject(input, new ValidationContext(input), new List<ValidationResult>(), true));
        }

        [Fact]
        public async Task Summary_ShouldAverageAllVotesAndProjectOnlyTheCurrentUsersRating()
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("rating-query-" + Guid.NewGuid()).Options);
            db.Recipes.AddRange(new Recipe { Id = 1, Name = "Rated", UserId = "author" }, new Recipe { Id = 2, Name = "Empty", UserId = "reader" });
            db.RecipeRatings.AddRange(new RecipeRating { RecipeId = 1, UserId = "reader", Stars = 5 }, new RecipeRating { RecipeId = 1, UserId = "other", Stars = 3 }, new RecipeRating { RecipeId = 1, UserId = "third", Stars = 3 });
            await db.SaveChangesAsync();
            var states = await RecipeRatingQueries.LoadAsync(db, new[] { 1, 1, 2 }, "reader");
            Assert.Equal(2, states.Count);
            Assert.Equal(11.0 / 3, states[1].AverageRating, 8);
            Assert.Equal(3, states[1].RatingCount);
            Assert.Equal(5, states[1].UserRating);
            Assert.True(states[1].CanRate);
            Assert.Equal(0, states[2].AverageRating);
            Assert.Equal(0, states[2].RatingCount);
            Assert.Null(states[2].UserRating);
            Assert.False(states[2].CanRate);
            var anonymous = await db.Recipes.SelectRatingStates(null).SingleAsync(recipe => recipe.RecipeId == 1);
            Assert.Null(anonymous.UserRating);
            Assert.False(anonymous.CanRate);
        }

        [Fact]
        public void SummaryQuery_ShouldTranslateToPostgresAggregatesWithoutIdentityColumns()
        {
            using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=cookshare_test;Username=postgres").Options);
            string sql = db.Recipes.Where(recipe => recipe.Id == 1).SelectRatingStates("reader").ToQueryString();
            Assert.Contains("avg(", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("count(", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AspNetUsers", sql);
            Assert.DoesNotContain("PasswordHash", sql);
            var rating = db.Model.FindEntityType(typeof(RecipeRating))!;
            Assert.Contains(rating.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "UserId", "RecipeId" }));
            Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
        }
    }
}
