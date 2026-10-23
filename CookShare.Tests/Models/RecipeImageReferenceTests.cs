using CookShare.Helpers;

namespace CookShare.Tests.Models
{
    public class RecipeImageReferenceTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("data:image/png;base64,aGVsbG8=")]
        [InlineData("javascript:alert(1)")]
        [InlineData("//images.example.com/pho.jpg")]
        [InlineData("https://user:password@images.example.com/pho.jpg")]
        [InlineData("/img/../private/pho.jpg")]
        [InlineData("/img/%2E%2E/private/pho.jpg")]
        [InlineData("/%2fimages.example.com/pho.jpg")]
        [InlineData("/img/%5cpho.jpg")]
        [InlineData("/img/pho\n.jpg")]
        public void MissingOrUnsafeReference_ShouldUsePlaceholder(string? reference)
        {
            Assert.Equal(RecipeImageReference.Placeholder, RecipeImageReference.GetSource(reference));
        }

        [Theory]
        [InlineData("https://images.example.com/pho.jpg", "https://images.example.com/pho.jpg")]
        [InlineData("http://images.example.com/pho.jpg", "http://images.example.com/pho.jpg")]
        [InlineData("/img/pho.webp", "/img/pho.webp")]
        [InlineData("~/img/pho.webp", "/img/pho.webp")]
        [InlineData("  /img/pho.webp  ", "/img/pho.webp")]
        public void SupportedReference_ShouldReturnUsableSource(string reference, string expected)
        {
            Assert.Equal(expected, RecipeImageReference.GetSource(reference));
        }

        [Fact]
        public void OversizedReference_ShouldUsePlaceholder()
        {
            Assert.Equal(RecipeImageReference.Placeholder,
                RecipeImageReference.GetSource("/" + new string('a', RecipeImageReference.MaxLength)));
        }
    }
}
