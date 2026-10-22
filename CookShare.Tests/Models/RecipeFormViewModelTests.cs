using System.ComponentModel.DataAnnotations;
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
        [InlineData(nameof(RecipeFormViewModel.Image), 2000)]
        public void FieldAtLengthLimit_ShouldPassValidation(string propertyName, int maxLength)
        {
            var model = CreateValidModel();
            typeof(RecipeFormViewModel).GetProperty(propertyName)!.SetValue(model, new string('a', maxLength));

            Assert.Empty(Validate(model));
        }

        [Theory]
        [InlineData(nameof(RecipeFormViewModel.Name), 200)]
        [InlineData(nameof(RecipeFormViewModel.Category), 100)]
        [InlineData(nameof(RecipeFormViewModel.Image), 2000)]
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
