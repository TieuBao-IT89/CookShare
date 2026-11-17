using CookShare.Data.Seeding;

namespace CookShare.Tests.Seeding
{
    public sealed class RecipeSeedOptionsTests
    {
        [Fact]
        public void Parse_ShouldSupportSourceCountUsersAndDryRun()
        {
            var options = RecipeSeedOptions.Parse(new[]
            {
                "--source", "TheMealDB", "--count", "5", "--users", "admintest1, root123", "--dry-run", "--search", "chicken soup"
            });

            Assert.Equal("themealdb", options.Source);
            Assert.Equal(5, options.Count);
            Assert.Equal(new[] { "admintest1", "root123" }, options.UserNames);
            Assert.True(options.DryRun);
            Assert.Equal("chicken soup", options.Search);
        }

        [Theory]
        [InlineData("--count", "0")]
        [InlineData("--count", "101")]
        [InlineData("--count", "abc")]
        [InlineData("--source", "unknown")]
        [InlineData("--users", "")]
        [InlineData("--users", "admin,")]
        [InlineData("--users", "Admin,admin")]
        [InlineData("--unknown", "value")]
        [InlineData("--search", "chicken")]
        public void Parse_ShouldRejectInvalidArguments(string option, string value)
        {
            Assert.Throws<ArgumentException>(() => RecipeSeedOptions.Parse(new[] { option, value }));
        }

        [Theory]
        [InlineData("--count")]
        [InlineData("--count", "--dry-run")]
        [InlineData("--dry-run", "--dry-run")]
        [InlineData("--source", "themealdb", "--search", "rice", "--meal-id", "52772")]
        [InlineData("--source", "themealdb", "--meal-id", "../secret")]
        public void Parse_ShouldRejectMissingDuplicateOrConflictingArguments(params string[] args)
        {
            Assert.Throws<ArgumentException>(() => RecipeSeedOptions.Parse(args));
        }
    }
}
