using CookShare.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace CookShare.Tests.Controllers
{
    public class RecipeControllerTests
    {
        [Fact]
        public void Index_ShouldReturnView()
        {
            // Arrange
            var controller = new RecipeController();

            // Act
            var result = controller.Index();

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public void Create_ShouldReturnView()
        {
            // Arrange
            var controller = new RecipeController();

            // Act
            var result = controller.Create();

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void Edit_ShouldSetCorrectRecipeId(int id)
        {
            // Arrange
            var controller = new RecipeController();

            // Act
            var result = controller.Edit(id);

            // Assert
            Assert.IsType<ViewResult>(result);
            Assert.Equal(id, controller.ViewData["RecipeId"]);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void Details_ShouldSetCorrectRecipeId(int id)
        {
            // Arrange
            var controller = new RecipeController();

            // Act
            var result = controller.Details(id);

            // Assert
            Assert.IsType<ViewResult>(result);
            Assert.Equal(id, controller.ViewData["RecipeId"]);
        }

        [Fact]
        public void Saved_ShouldReturnView()
        {
            // Arrange
            var controller = new RecipeController();

            // Act
            var result = controller.Saved();

            // Assert
            Assert.IsType<ViewResult>(result);
        }
    }
}
