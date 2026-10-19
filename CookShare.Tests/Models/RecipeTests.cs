using System.ComponentModel.DataAnnotations;
using CookShare.Models;

namespace CookShare.Tests.Models
{
    public class RecipeTests
    {
        // Tạo công thức hợp lệ để sử dụng trong các bài test.
        private Recipe CreateValidRecipe()
        {
            return new Recipe
            {
                Name = "Phở bò",
                Description = "Món ăn truyền thống Việt Nam",
                Ingredients = "Bánh phở, thịt bò, nước dùng",
                Instructions = "Nấu nước dùng và trụng bánh phở",
                Category = "Món chính"
            };
        }

        // Kiểm tra dữ liệu bằng Data Annotations.
        private List<ValidationResult> ValidateRecipe(Recipe recipe)
        {
            var results = new List<ValidationResult>();

            var context = new ValidationContext(recipe);

            Validator.TryValidateObject(
                recipe,
                context,
                results,
                validateAllProperties: true
            );

            return results;
        }

        [Fact]
        public void ValidRecipe_ShouldPassValidation()
        {
            // Arrange
            var recipe = CreateValidRecipe();

            // Act
            var results = ValidateRecipe(recipe);

            // Assert
            Assert.Empty(results);
        }

        [Theory]
        [InlineData("Name")]
        [InlineData("Description")]
        [InlineData("Ingredients")]
        [InlineData("Instructions")]
        [InlineData("Category")]
        public void EmptyRequiredField_ShouldFailValidation(string propertyName)
        {
            // Arrange
            var recipe = CreateValidRecipe();

            typeof(Recipe)
                .GetProperty(propertyName)!
                .SetValue(recipe, string.Empty);

            // Act
            var results = ValidateRecipe(recipe);

            // Assert
            Assert.Contains(results, result =>
                result.MemberNames.Contains(propertyName));
        }

        [Fact]
        public void EmptyImage_ShouldPassValidation()
        {
            // Arrange
            var recipe = CreateValidRecipe();
            recipe.Image = null;

            // Act
            var results = ValidateRecipe(recipe);

            // Assert
            Assert.Empty(results);
        }
    }
}
