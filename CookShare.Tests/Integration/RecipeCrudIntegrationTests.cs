
using System.Net;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CookShare.Tests.Integration
{
    [Collection(PostgreSqlIntegrationCollection.Name)]
    public class RecipeCrudIntegrationTests
    {
        private readonly CookShareWebApplicationFactory _factory;

        public RecipeCrudIntegrationTests(
            CookShareWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // Lấy Anti-Forgery Token từ form Create.
        private static async Task<string> GetTokenAsync(
            HttpClient client)
        {
            using var response = await client.GetAsync(
                "/Recipe/Create");

            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();

            var input = Regex.Matches(
                    html, @"<input\b[^>]*>",
                    RegexOptions.IgnoreCase)
                .Select(m => m.Value)
                .FirstOrDefault(tag =>
                    tag.Contains(
                        "name=\"__RequestVerificationToken\"",
                        StringComparison.OrdinalIgnoreCase));

            if (input == null)
            {
                throw new InvalidOperationException(
                    "Anti-Forgery Token not found.");
            }

            var value = Regex.Match(
                input,
                "value=\"([^\"]+)\"",
                RegexOptions.IgnoreCase);

            if (!value.Success)
            {
                throw new InvalidOperationException(
                    "Anti-Forgery Token has no value.");
            }

            return WebUtility.HtmlDecode(value.Groups[1].Value);
        }

        private static Dictionary<string, string>
            ValidRecipeForm(string name)
        {
            return new Dictionary<string, string>
            {
                ["Name"] = name,
                ["Description"] = "Cong thuc kiem thu",
                ["Category"] = "Món Việt",
                ["Servings"] = "4",
                ["Ingredients"] = "Thit bo\nBanh pho",
                ["Instructions"] = "Buoc 1: Chuan bi\nBuoc 2: Nau"
            };
        }

        private static async Task<HttpResponseMessage> PostFormAsync(
            HttpClient client,
            string url,
            Dictionary<string, string> fields,
            bool includeToken = true)
        {
            if (includeToken)
            {
                fields["__RequestVerificationToken"] =
                    await GetTokenAsync(client);
            }

            return await client.PostAsync(
                url,
                new FormUrlEncodedContent(fields));
        }

        // Chèn dữ liệu thử trực tiếp vào database.
        private async Task<int> SeedRecipeAsync(
            string userId,
            string? name = null)
        {
            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var recipe = new Recipe
            {
                UserId = userId,
                Name = name ?? "Recipe " + Guid.NewGuid(),
                Description = "Test description",
                Category = "Món Việt",
                Ingredients = "Thit bo",
                Instructions = "Nau chin",
                CreatedAt = DateTime.UtcNow
            };

            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();

            return recipe.Id;
        }

        private async Task<Recipe?> FindRecipeAsync(int id)
        {
            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            return await db.Recipes
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        // TC01 - Tạo Recipe hợp lệ.
        [PostgresFact]
        public async Task TC01_CreateValidRecipe_ShouldSaveToDatabase()
        {
            using var client =
                _factory.CreateTestClient(_factory.OwnerId);

            var name = "Test Recipe " + Guid.NewGuid();
            var form = ValidRecipeForm(name);
            form["Servings"] = "6";

            using var response = await PostFormAsync(
                client,
                "/Recipe/Create",
                form);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var saved = await db.Recipes
                .AsNoTracking()
                .SingleAsync(r => r.Name == name);

            Assert.Equal(_factory.OwnerId, saved.UserId);
            Assert.Equal(6, saved.Servings);
        }

        // TC02 - Tạo Recipe thiếu Name.
        [PostgresFact]
        public async Task TC02_MissingName_ShouldBeRejected()
        {
            using var client =
                _factory.CreateTestClient(_factory.OwnerId);

            var form = ValidRecipeForm("");
            form["Description"] =
                "Invalid " + Guid.NewGuid();

            using var response = await PostFormAsync(
                client, "/Recipe/Create", form);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            Assert.False(await db.Recipes.AnyAsync(
                r => r.Description == form["Description"]));
        }

        // TC03 - Tạo Recipe khi chưa đăng nhập.
        [PostgresFact]
        public async Task TC03_CreateAnonymous_ShouldRedirectToLogin()
        {
            using var client = _factory.CreateTestClient();

            using var response = await client.GetAsync(
                "/Recipe/Create");

            Assert.Equal(
                HttpStatusCode.Redirect,
                response.StatusCode);

            Assert.Contains(
                "/Account/Login",
                response.Headers.Location?.ToString() ?? "");
        }

        // TC04 - Danh sách lấy từ database.
        [PostgresFact]
        public async Task TC04_Index_ShouldShowDatabaseRecipe()
        {
            var name = "Index Test " + Guid.NewGuid();

            await SeedRecipeAsync(_factory.OwnerId, name);

            using var client = _factory.CreateTestClient();

            using var response = await client.GetAsync("/Recipe");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains(name, html);
        }

        // TC05 - ID không tồn tại.
        [PostgresFact]
        public async Task TC05_DetailsMissing_ShouldReturn404()
        {
            using var client = _factory.CreateTestClient();

            using var response = await client.GetAsync(
                "/Recipe/Details/2147483647");

            Assert.Equal(
                HttpStatusCode.NotFound,
                response.StatusCode);
        }

        // TC06 - Chủ sở hữu sửa công thức.
        [PostgresFact]
        public async Task TC06_OwnerEdit_ShouldUpdateDatabase()
        {
            var id = await SeedRecipeAsync(_factory.OwnerId);

            using var client =
                _factory.CreateTestClient(_factory.OwnerId);

            var newName = "Updated " + Guid.NewGuid();
            var form = ValidRecipeForm(newName);
            form["Servings"] = "7";

            using var response = await PostFormAsync(
                client,
                $"/Recipe/Edit/{id}",
                form);

            Assert.Equal(
                HttpStatusCode.Redirect,
                response.StatusCode);

            var updated = await FindRecipeAsync(id);

            Assert.NotNull(updated);
            Assert.Equal(newName, updated.Name);
            Assert.Equal(7, updated.Servings);
        }

        // TC07 - Người khác không được sửa.
        [PostgresFact]
        public async Task TC07_OtherUserEdit_ShouldReturn403()
        {
            var id = await SeedRecipeAsync(_factory.OwnerId);

            using var client =
                _factory.CreateTestClient(_factory.OtherUserId);

            using var response = await PostFormAsync(
                client,
                $"/Recipe/Edit/{id}",
                ValidRecipeForm("Unauthorized edit"));

            Assert.Equal(
                HttpStatusCode.Forbidden,
                response.StatusCode);
        }

        // TC08 - Chủ sở hữu xóa công thức.
        [PostgresFact]
        public async Task TC08_OwnerDelete_ShouldRemoveRecipe()
        {
            var id = await SeedRecipeAsync(_factory.OwnerId);

            using var client =
                _factory.CreateTestClient(_factory.OwnerId);

            using var response = await PostFormAsync(
                client,
                $"/Recipe/Delete/{id}",
                new Dictionary<string, string>());

            Assert.Equal(
                HttpStatusCode.Redirect,
                response.StatusCode);

            var deleted = await FindRecipeAsync(id);

            Assert.Null(deleted);
        }

        // TC09 - Người khác không được xóa.
        [PostgresFact]
        public async Task TC09_OtherUserDelete_ShouldReturn403()
        {
            var id = await SeedRecipeAsync(_factory.OwnerId);

            using var client =
                _factory.CreateTestClient(_factory.OtherUserId);

            using var response = await PostFormAsync(
                client,
                $"/Recipe/Delete/{id}",
                new Dictionary<string, string>());

            Assert.Equal(
                HttpStatusCode.Forbidden,
                response.StatusCode);

            Assert.NotNull(await FindRecipeAsync(id));
        }

        // TC10 - POST không có Anti-Forgery Token.
        [PostgresFact]
        public async Task TC10_MissingAntiForgery_ShouldReturn400()
        {
            using var client =
                _factory.CreateTestClient(_factory.OwnerId);

            using var response = await PostFormAsync(
                client,
                "/Recipe/Create",
                ValidRecipeForm("Invalid token test"),
                includeToken: false);

            Assert.Equal(
                HttpStatusCode.BadRequest,
                response.StatusCode);
        }
    }
}
