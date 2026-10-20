using CookShare.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace CookShare.Tests.Controllers
{
    public class ProfileControllerTests
    {
        [Fact]
        public void Index_WithoutUserId_ShouldReturnViewWithEmptyTargetUserId()
        {
            // Arrange
            var controller = new ProfileController();

            // Act
            var result = controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(string.Empty, controller.ViewData["TargetUserId"]);
        }

        [Theory]
        [InlineData("demo-nguyen-van-an")]
        [InlineData("demo-tran-minh-anh")]
        [InlineData("demo-le-hoang-nam")]
        public void Index_WithUserId_ShouldReturnViewWithTargetUserId(string userId)
        {
            // Arrange
            var controller = new ProfileController();

            // Act
            var result = controller.Index(userId);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(userId, controller.ViewData["TargetUserId"]);
        }
    }
}
