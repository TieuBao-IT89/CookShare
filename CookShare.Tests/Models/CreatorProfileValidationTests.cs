using System.ComponentModel.DataAnnotations;
using CookShare.Helpers;
using CookShare.ViewModels;

namespace CookShare.Tests.Models
{
    public sealed class CreatorProfileValidationTests
    {
        [Theory]
        [InlineData(null, true)]
        [InlineData("Yêu bếp Việt\nMỗi món ăn là một câu chuyện <3", true)]
        [InlineData("<script>alert(1)</script>", false)]
        [InlineData("<img src=x onerror=alert(1)>", false)]
        [InlineData("< svg onload=alert(1)>", false)]
        [InlineData("<!-- comment -->", false)]
        [InlineData("Text\0with invalid control", false)]
        public void Bio_ShouldAllowVietnameseAndNewlinesButRejectMarkup(string? bio, bool valid)
        {
            var model = new EditProfileViewModel { DisplayName = "Căn bếp Việt", Bio = bio };
            Assert.Equal(valid, Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), true));
        }

        [Fact]
        public void Bio_ShouldNormalizeFormLineEndingsAndEnforce300Characters()
        {
            var model = new EditProfileViewModel { DisplayName = "Căn bếp", Bio = "Yêu bếp\r\n" + new string('a', 292) };
            Assert.Equal(300, model.Bio!.Length);
            Assert.DoesNotContain('\r', model.Bio);
            Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), true));
            model.Bio += "a";
            Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), true));
        }

        [Theory]
        [InlineData("/img/recipes/pho.webp", true)]
        [InlineData("~/img/recipes/pho.webp", true)]
        [InlineData("https://example.com/cover.webp", true)]
        [InlineData("http://example.com/cover.webp", false)]
        [InlineData("//evil.example/cover", false)]
        [InlineData("/\\evil.example/cover", false)]
        [InlineData("/%2fexample.com/cover", false)]
        [InlineData("/img/%2e%2e/private", false)]
        [InlineData("javascript:alert(1)", false)]
        [InlineData("data:image/svg+xml,<svg/>", false)]
        [InlineData("https://user:password@example.com/a", false)]
        public void ProfileImages_ShouldUseSafeLocalOrHttpsReferences(string source, bool valid)
        {
            Assert.Equal(valid, ProfileContent.IsValidImage(source));
            Assert.Equal(valid, ProfileContent.ImageSource(source) != null);
            var model = new EditProfileViewModel { DisplayName = "Căn bếp", CoverImageUrl = source };
            Assert.Equal(valid, Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), true));
        }
    }
}
