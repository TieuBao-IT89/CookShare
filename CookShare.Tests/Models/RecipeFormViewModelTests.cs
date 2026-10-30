using System.ComponentModel.DataAnnotations;
using CookShare.Models;
using CookShare.ViewModels;

namespace CookShare.Tests.Models
{
    public class RecipeFormViewModelTests
    {
        [Fact]
        public void ValidRecipe_ShouldPassValidation()
        {
            Assert.Empty(Validate(CreateValidModel()));
        }

        [Theory]
        [InlineData(nameof(RecipeFormViewModel.PreparationTimeMinutes), 0)]
        [InlineData(nameof(RecipeFormViewModel.PreparationTimeMinutes), 1441)]
        [InlineData(nameof(RecipeFormViewModel.CookingTimeMinutes), 0)]
        [InlineData(nameof(RecipeFormViewModel.CookingTimeMinutes), 1441)]
        public void InvalidRecipeTime_ShouldFailValidation(string propertyName, int minutes)
        {
            var model = CreateValidModel();
            typeof(RecipeFormViewModel).GetProperty(propertyName)!.SetValue(model, minutes);

            Assert.Contains(
                Validate(model),
                error => error.MemberNames.Contains(propertyName));
        }

        [Fact]
        public void UnknownDifficultyValue_ShouldFailValidation()
        {
            var model = CreateValidModel();
            model.Difficulty = (RecipeDifficulty)99;

            Assert.Contains(
                Validate(model),
                error => error.MemberNames.Contains(nameof(model.Difficulty)));
        }

        [Theory]
        [InlineData(nameof(RecipeFormViewModel.Name), null)]
        [InlineData(nameof(RecipeFormViewModel.Name), "")]
        [InlineData(nameof(RecipeFormViewModel.Name), "   ")]
        [InlineData(nameof(RecipeFormViewModel.Description), null)]
        [InlineData(nameof(RecipeFormViewModel.Description), "")]
        [InlineData(nameof(RecipeFormViewModel.Description), "   ")]
        [InlineData(nameof(RecipeFormViewModel.Category), null)]
        [InlineData(nameof(RecipeFormViewModel.Category), "")]
        [InlineData(nameof(RecipeFormViewModel.Category), "   ")]
        [InlineData(nameof(RecipeFormViewModel.Ingredients), null)]
        [InlineData(nameof(RecipeFormViewModel.Ingredients), "")]
        [InlineData(nameof(RecipeFormViewModel.Ingredients), "   ")]
        [InlineData(nameof(RecipeFormViewModel.Instructions), null)]
        [InlineData(nameof(RecipeFormViewModel.Instructions), "")]
        [InlineData(nameof(RecipeFormViewModel.Instructions), "   ")]
        public void MissingRequiredField_ShouldFailValidation(string propertyName, string? value)
        {
            var model = CreateValidModel();
            typeof(RecipeFormViewModel).GetProperty(propertyName)!.SetValue(model, value);

            var error = Assert.Single(Validate(model));

            Assert.Contains(propertyName, error.MemberNames);
        }

        [Theory]
        [InlineData(nameof(RecipeFormViewModel.Name), 200)]
        [InlineData(nameof(RecipeFormViewModel.Category), 100)]
        [InlineData(nameof(RecipeFormViewModel.Description), 2000)]
        [InlineData(nameof(RecipeFormViewModel.Ingredients), 10000)]
        [InlineData(nameof(RecipeFormViewModel.Instructions), 20000)]
        public void FieldAtLengthLimit_ShouldPassValidation(string propertyName, int maxLength)
        {
            var model = CreateValidModel();
            typeof(RecipeFormViewModel).GetProperty(propertyName)!.SetValue(model, new string('a', maxLength));

            Assert.Empty(Validate(model));
        }

        [Theory]
        [InlineData(nameof(RecipeFormViewModel.Name), 200)]
        [InlineData(nameof(RecipeFormViewModel.Category), 100)]
        [InlineData(nameof(RecipeFormViewModel.Description), 2000)]
        [InlineData(nameof(RecipeFormViewModel.Ingredients), 10000)]
        [InlineData(nameof(RecipeFormViewModel.Instructions), 20000)]
        public void FieldOverLengthLimit_ShouldFailValidation(string propertyName, int maxLength)
        {
            var model = CreateValidModel();
            typeof(RecipeFormViewModel).GetProperty(propertyName)!.SetValue(model, new string('a', maxLength + 1));

            var error = Assert.Single(Validate(model));

            Assert.Contains(propertyName, error.MemberNames);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void EmptyImage_ShouldPassValidation(string? image)
        {
            var model = CreateValidModel();
            model.Image = image;

            Assert.Empty(Validate(model));
        }

        [Theory]
        [InlineData("https://images.example.com/pho.jpg?width=800")]
        [InlineData("http://images.example.com/pho.jpg")]
        [InlineData("/img/recipes/pho.webp")]
        [InlineData("~/img/recipes/pho.webp")]
        public void SupportedImageReference_ShouldPassValidation(string image)
        {
            var model = CreateValidModel();
            model.Image = image;

            Assert.Empty(Validate(model));
        }

        [Theory]
        [InlineData("data:image/png;base64,aGVsbG8=")]
        [InlineData("javascript:alert(1)")]
        [InlineData("file:///C:/photos/pho.jpg")]
        [InlineData("//images.example.com/pho.jpg")]
        [InlineData("http:")]
        [InlineData("not an image reference")]
        [InlineData("/img/../private/pho.jpg")]
        [InlineData("/img/%2e%2e/private/pho.jpg")]
        [InlineData("/\\images.example.com/pho.jpg")]
        public void UnsupportedImageReference_ShouldFailValidation(string image)
        {
            var model = CreateValidModel();
            model.Image = image;

            Assert.Contains(Validate(model), error => error.MemberNames.Contains(nameof(model.Image)));
        }

        [Fact]
        public void ImageAtLengthLimit_ShouldPassValidation()
        {
            var model = CreateValidModel();
            model.Image = "/" + new string('a', 1999);

            Assert.Empty(Validate(model));
        }

        [Fact]
        public void ImageOverLengthLimit_ShouldFailValidation()
        {
            var model = CreateValidModel();
            model.Image = "/" + new string('a', 2000);

            Assert.Contains(Validate(model), error => error.MemberNames.Contains(nameof(model.Image)));
        }

        private static RecipeFormViewModel CreateValidModel()
        {
            return new RecipeFormViewModel
            {
                Name = "Phở bò",
                Description = "Phở bò truyền thống",
                Category = "Món Việt",
                Ingredients = "Bánh phở, thịt bò",
                Instructions = "Nấu nước dùng",
                Image = "/img/recipe-placeholder.svg"
            };
        }

        private static List<ValidationResult> Validate(RecipeFormViewModel model)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(
                model,
                new ValidationContext(model),
                results,
                validateAllProperties: true);
            return results;
        }
    }
}
