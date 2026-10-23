
using System.ComponentModel.DataAnnotations;
using CookShare.ViewModels;

namespace CookShare.Tests.Models
{
    public class EditProfileViewModelTests
    {
        private static bool IsValid(EditProfileViewModel model)
        {
            var results = new List<ValidationResult>();

            return Validator.TryValidateObject(
                model,
                new ValidationContext(model),
                results,
                validateAllProperties: true);
        }

        [Fact]
        public void ValidProfile_ShouldPassValidation()
        {
            var model = new EditProfileViewModel
            {
                DisplayName = "Nguyen Quoc Bao",
                AvatarUrl = "/img/profile.webp"
            };

            Assert.True(IsValid(model));
        }

        [Fact]
        public void EmptyDisplayName_ShouldFailValidation()
        {
            var model = new EditProfileViewModel
            {
                DisplayName = ""
            };

            Assert.False(IsValid(model));
        }

        [Fact]
        public void TooLongDisplayName_ShouldFailValidation()
        {
            var model = new EditProfileViewModel
            {
                DisplayName = new string('A', 101)
            };

            Assert.False(IsValid(model));
        }

        [Fact]
        public void TooLongAvatarUrl_ShouldFailValidation()
        {
            var model = new EditProfileViewModel
            {
                DisplayName = "Test User",
                AvatarUrl = new string('a', 501)
            };

            Assert.False(IsValid(model));
        }
    }
}
