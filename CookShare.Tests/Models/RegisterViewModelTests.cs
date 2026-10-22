using System.ComponentModel.DataAnnotations;
using CookShare.ViewModels;

namespace CookShare.Tests.Models
{
    public class RegisterViewModelTests
    {
        private static bool IsValid(RegisterViewModel model)
        {
            var results = new List<ValidationResult>();

            return Validator.TryValidateObject(
                model,
                new ValidationContext(model),
                results,
                validateAllProperties: true);
        }

        [Fact]
        public void ValidRegisterModel_ShouldPass()
        {
            var model = new RegisterViewModel
            {
                LastName = "Nguyen",
                FirstName = "Test",
                UserName = "testuser01",
                Email = "test@example.com",
                Password = "TestPassword123",
                ConfirmPassword = "TestPassword123"
            };

            Assert.True(IsValid(model));
        }

        [Fact]
        public void InvalidEmail_ShouldFail()
        {
            var model = new RegisterViewModel
            {
                LastName = "Nguyen",
                FirstName = "Test",
                UserName = "testuser01",
                Email = "invalid-email",
                Password = "TestPassword123",
                ConfirmPassword = "TestPassword123"
            };

            Assert.False(IsValid(model));
        }

        [Fact]
        public void DifferentPasswords_ShouldFail()
        {
            var model = new RegisterViewModel
            {
                LastName = "Nguyen",
                FirstName = "Test",
                UserName = "testuser01",
                Email = "test@example.com",
                Password = "TestPassword123",
                ConfirmPassword = "Different123"
            };

            Assert.False(IsValid(model));
        }
    }
}
