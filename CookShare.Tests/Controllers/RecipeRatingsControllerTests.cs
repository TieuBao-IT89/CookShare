using System.Security.Claims;
using CookShare.Controllers;
using CookShare.Data;
using CookShare.Models;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Tests.Controllers
{
    public sealed class RecipeRatingsControllerTests
    {
        [Fact]
        public async Task FailedSave_ShouldReturnSafe503WithoutPersistingTheVote()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase("rating-failure-" + Guid.NewGuid()).Options;
            await using (var seed = new ApplicationDbContext(options))
            {
                seed.Recipes.Add(new Recipe { Id = 12, UserId = "author", Name = "Recipe" });
                await seed.SaveChangesAsync();
            }

            await using var failing = new FailingRatingContext(options);
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "reader") }, "Test"))
            };
            context.Request.Headers.Accept = "application/json";
            var controller = new RecipeRatingsController(failing) { ControllerContext = new ControllerContext { HttpContext = context } };
            var result = Assert.IsType<ObjectResult>(await controller.Save(12, new RecipeRatingFormViewModel { Stars = 4 }, default));
            Assert.Equal(503, result.StatusCode);
            Assert.DoesNotContain("private", System.Text.Json.JsonSerializer.Serialize(result.Value));
            await using var verify = new ApplicationDbContext(options);
            Assert.Empty(await verify.RecipeRatings.ToListAsync());
        }

        private sealed class FailingRatingContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
        {
            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                throw new DbUpdateException("private connection details should never reach the response");
            }
        }
    }
}
