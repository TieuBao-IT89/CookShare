using CookShare.Data;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Tests.Models
{
    public class RecipeQueryTests
    {
        [Fact]
        public void ExploreFilters_ShouldTranslateToPostgreSqlWithoutOpeningConnection()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=cookshare_query_translation;Username=query_test")
                .Options;
            using var db = new ApplicationDbContext(options);

            string sql = db.Recipes
                .FilterBySearchAndCategory("PHO", "MÓN VIỆT")
                .ToQueryString();

            Assert.Contains("WHERE", sql);
            Assert.Matches("lower\\(\\w+\\.\"Name\"\\)", sql);
            Assert.Matches("lower\\(\\w+\\.\"Category\"\\)", sql);
            Assert.Contains("AND", sql);
            Assert.Contains("pho", sql);
            Assert.Contains("món việt", sql);
            Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
        }
    }
}
