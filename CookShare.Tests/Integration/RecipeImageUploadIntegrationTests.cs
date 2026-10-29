using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using CookShare.Data;
using CookShare.Helpers;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CookShare.Tests.Integration
{
    public sealed class RecipeImageUploadIntegrationTests : IAsyncLifetime
    {
        private const int MaximumSize = 5 * 1024 * 1024;
        private const string OwnerId = "image-upload-owner";
        private const string OtherId = "image-upload-other";
        private readonly UploadFactory _factory = new();
        private static readonly byte[] _png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");

        public async Task InitializeAsync()
        {
            await _factory.InitializeAsync();
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", db.Database.ProviderName);
            db.Users.AddRange(new ApplicationUser { Id = OwnerId, UserName = OwnerId },
                new ApplicationUser { Id = OtherId, UserName = OtherId });
            await db.SaveChangesAsync();
        }

        public async Task DisposeAsync() => await _factory.DisposeAsync();

        [Theory]
        [InlineData("png", "image/png")]
        [InlineData("jpeg", "image/jpeg")]
        [InlineData("webp", "image/webp")]
        public async Task Create_upload_persists_image_with_unique_names_and_allows_public_read(string extension, string contentType)
        {
            var bytes = ImageBytes(extension);
            using var client = _factory.CreateTestClient(OwnerId);
            var name = "Upload " + extension;
            using var first = await PostAsync(client, "/Recipe/Create", name, bytes, "meal." + extension, contentType);
            using var second = await PostAsync(client, "/Recipe/Create", name + " duplicate", bytes, "meal." + extension, contentType);

            Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
            var saved = await FindAsync(name);
            var duplicate = await FindAsync(name + " duplicate");
            Assert.Equal(OwnerId, saved.UserId);
            Assert.Matches("^/uploads/recipes/[a-f0-9]{32}\\.(jpg|png|webp)$", saved.Image!);
            Assert.NotEqual(saved.Image, duplicate.Image);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(_factory.StoragePath, Path.GetFileName(saved.Image!))));
            Assert.Equal(2, StoredFiles().Length);

            using var anonymous = _factory.CreateTestClient();
            using var image = await anonymous.GetAsync(saved.Image);
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal(contentType, image.Content.Headers.ContentType?.MediaType);
            Assert.Equal("nosniff", Assert.Single(image.Headers.GetValues("X-Content-Type-Options")));
            Assert.Equal(bytes, await image.Content.ReadAsByteArrayAsync());
            using var details = await anonymous.GetAsync(first.Headers.Location);
            Assert.Contains(saved.Image!, await details.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("../../outside.png")]
        [InlineData("C:\\fakepath\\meal.png")]
        public async Task Client_filenames_never_choose_the_destination_path(string filename)
        {
            using var client = _factory.CreateTestClient(OwnerId);
            using var response = await PostAsync(client, "/Recipe/Create", "Untrusted filename", _png, filename, "image/png");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var saved = await FindAsync("Untrusted filename");
            Assert.Matches("^/uploads/recipes/[a-f0-9]{32}\\.png$", saved.Image!);
            Assert.Equal(Path.GetFileName(saved.Image!), Path.GetFileName(Assert.Single(StoredFiles())));
            Assert.Equal(_png, await File.ReadAllBytesAsync(Assert.Single(StoredFiles())));
        }

        [Theory]
        [InlineData("script.svg", "image/svg+xml", "svg")]
        [InlineData("script.html", "image/png", "png")]
        [InlineData("pretend.png", "image/png", "html")]
        [InlineData("wrong.jpg", "image/jpeg", "png")]
        [InlineData("wrong.webp", "image/webp", "png")]
        [InlineData("empty.png", "image/png", "empty")]
        public async Task Unsupported_or_spoofed_images_are_rejected_without_database_or_file_writes(string filename, string contentType, string payload)
        {
            byte[] bytes = payload switch
            {
                "png" => _png,
                "svg" => Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>"),
                "empty" => Array.Empty<byte>(),
                _ => Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>")
            };
            using var client = _factory.CreateTestClient(OwnerId);
            using var response = await PostAsync(client, "/Recipe/Create", "Invalid image", bytes, filename, contentType);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("ImageFile", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            Assert.Equal(0, await RecipeCountAsync());
            Assert.Empty(StoredFiles());
            Assert.False(Directory.Exists(_factory.StoragePath));
        }

        [Fact]
        public async Task Oversized_file_is_rejected_without_an_orphan_file()
        {
            var oversized = new byte[MaximumSize + 1];
            _png.CopyTo(oversized, 0);
            using var client = _factory.CreateTestClient(OwnerId);
            using var response = await PostAsync(client, "/Recipe/Create", "Oversized image", oversized, "large.png", "image/png");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, await RecipeCountAsync());
            Assert.Empty(StoredFiles());
        }

        [Fact]
        public async Task File_at_the_five_MiB_limit_is_accepted()
        {
            var bytes = new byte[MaximumSize];
            _png.CopyTo(bytes, 0);
            _png.AsSpan(_png.Length - 12).CopyTo(bytes.AsSpan(bytes.Length - 12));
            using var client = _factory.CreateTestClient(OwnerId);
            using var response = await PostAsync(client, "/Recipe/Create", "Boundary image", bytes, "boundary.png", "image/png");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(MaximumSize, new FileInfo(Assert.Single(StoredFiles())).Length);
            Assert.NotNull((await FindAsync("Boundary image")).Image);
        }

        [Fact]
        public async Task Storage_checks_actual_stream_size_even_when_declared_length_is_smaller()
        {
            var oversized = new byte[MaximumSize + 1];
            _png.CopyTo(oversized, 0);
            using var scope = _factory.Services.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IRecipeImageStorage>();

            await Assert.ThrowsAsync<RecipeImageUploadException>(() => storage.SaveAsync(new MisreportedFormFile(oversized)));

            Assert.Empty(StoredFiles());
        }

        [Fact]
        public async Task Creating_without_a_file_keeps_the_existing_placeholder_behavior()
        {
            using var client = _factory.CreateTestClient(OwnerId);
            using var response = await PostAsync(client, "/Recipe/Create", "No image");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Null((await FindAsync("No image")).Image);
            using var details = await client.GetAsync(response.Headers.Location);
            Assert.Contains(RecipeImageReference.Placeholder, await details.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            Assert.False(Directory.Exists(_factory.StoragePath));
        }

        [Fact]
        public async Task Editing_without_a_new_file_preserves_the_previous_image()
        {
            using var client = _factory.CreateTestClient(OwnerId);
            using var created = await PostAsync(client, "/Recipe/Create", "Before edit", _png, "meal.png", "image/png");
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            var original = await FindAsync("Before edit");

            using var edited = await PostAsync(client, "/Recipe/Edit/" + original.Id, "After edit");

            Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);
            Assert.Equal(original.Image, (await FindAsync("After edit")).Image);
            Assert.Single(StoredFiles());
            using var image = await client.GetAsync(original.Image);
            Assert.Equal(_png, await image.Content.ReadAsByteArrayAsync());
        }

        [Fact]
        public async Task Editing_with_a_new_file_updates_only_the_owned_recipe()
        {
            int target = await SeedRecipeAsync(OwnerId);
            int unrelated = await SeedRecipeAsync(OtherId);
            using var client = _factory.CreateTestClient(OwnerId);

            using var edited = await PostAsync(client, "/Recipe/Edit/" + target, "Replaced image", _png, "replacement.png", "image/png");

            Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);
            Assert.Matches("^/uploads/recipes/[a-f0-9]{32}\\.png$", (await FindAsync("Replaced image")).Image!);
            using var scope = _factory.Services.CreateScope();
            var other = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Recipes.AsNoTracking().SingleAsync(recipe => recipe.Id == unrelated);
            Assert.Equal("/img/recipes/pho.webp", other.Image);
            Assert.Single(StoredFiles());
        }

        [Fact]
        public async Task Authentication_ownership_and_antiforgery_are_enforced_before_storage_is_touched()
        {
            int ownedRecipe = await SeedRecipeAsync(OwnerId);
            using var anonymous = _factory.CreateTestClient();
            using var other = _factory.CreateTestClient(OtherId);
            using var owner = _factory.CreateTestClient(OwnerId);
            using var unauthenticated = await PostAsync(anonymous, "/Recipe/Create", "Anonymous upload", _png, "meal.png", "image/png", includeToken: false);
            using var forbidden = await PostAsync(other, "/Recipe/Edit/" + ownedRecipe, "Foreign upload", _png, "meal.png", "image/png");
            using var noToken = await PostAsync(owner, "/Recipe/Edit/" + ownedRecipe, "Missing token", _png, "meal.png", "image/png", includeToken: false);

            Assert.Equal(HttpStatusCode.Redirect, unauthenticated.StatusCode);
            Assert.Contains("/Account/Login", unauthenticated.Headers.Location?.ToString());
            Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
            Assert.Contains("/Account/AccessDenied", forbidden.Headers.Location?.ToString());
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
            Assert.Equal(1, await RecipeCountAsync());
            Assert.False(Directory.Exists(_factory.StoragePath));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Database_save_failure_removes_the_new_file_and_preserves_existing_data(bool editing)
        {
            int id = editing ? await SeedRecipeAsync(OwnerId) : 0;
            using var client = _factory.CreateTestClient(OwnerId);
            _factory.SaveFailure.Enabled = true;

            using var response = await PostAsync(client, editing ? "/Recipe/Edit/" + id : "/Recipe/Create", "Failed upload", _png, "meal.png", "image/png");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(_factory.SaveFailure.SawImageBeforeFailure);
            Assert.Empty(StoredFiles());
            Assert.Equal(editing ? 1 : 0, await RecipeCountAsync());
            if (editing)
            {
                using var scope = _factory.Services.CreateScope();
                var unchanged = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Recipes.AsNoTracking().SingleAsync();
                Assert.Equal("Seed recipe " + OwnerId, unchanged.Name);
                Assert.Equal("/img/recipes/pho.webp", unchanged.Image);
            }
        }

        [Fact]
        public async Task Public_upload_route_serves_only_generated_image_names()
        {
            Directory.CreateDirectory(_factory.StoragePath);
            await File.WriteAllTextAsync(Path.Combine(_factory.StoragePath, "secret.txt"), "Must never be served");
            using var client = _factory.CreateTestClient();

            foreach (string path in new[] { "/uploads/recipes/secret.txt", "/uploads/recipes/meal.png", "/uploads/recipes/" + Guid.NewGuid().ToString("N") + ".svg", "/uploads/recipes/" + Guid.NewGuid().ToString("N") + ".png" })
            {
                using var response = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                Assert.DoesNotContain("Must never be served", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            }
        }

        private string[] StoredFiles() => Directory.Exists(_factory.StoragePath)
            ? Directory.GetFiles(_factory.StoragePath, "*", SearchOption.AllDirectories)
            : Array.Empty<string>();

        private async Task<int> RecipeCountAsync()
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Recipes.CountAsync();
        }

        private async Task<Recipe> FindAsync(string name)
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Recipes.AsNoTracking().SingleAsync(recipe => recipe.Name == name);
        }

        private async Task<int> SeedRecipeAsync(string ownerId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var recipe = new Recipe
            {
                Name = "Seed recipe " + ownerId,
                Description = "Description",
                Ingredients = "Rice",
                Instructions = "Cook",
                Category = "Món Việt",
                UserId = ownerId,
                Image = "/img/recipes/pho.webp"
            };
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            return recipe.Id;
        }

        private static byte[] ImageBytes(string extension)
        {
            if (extension == "png")
            {
                return _png;
            }

            string repository = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(repository, "CookShare.sln")))
            {
                repository = Directory.GetParent(repository)?.FullName ?? throw new InvalidOperationException("Repository was not found.");
            }

            if (extension == "webp")
            {
                return File.ReadAllBytes(Path.Combine(repository, "CookShare", "wwwroot", "img", "recipes", "pho.webp"));
            }

            return Convert.FromBase64String("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////2wBDAf//////////////////////////////////////////////////////////////////////////////////////wAARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAX/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIQAxAAAAF//8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABBQJ//8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAgBAwEBPwF//8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAgBAgEBPwF//8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQAGPwJ//8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPyF//9oADAMBAAIAAwAAABAf/8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAgBAwEBPxB//8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAgBAgEBPxB//8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxB//9k=");
        }

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string action, string name,
            byte[]? bytes = null, string filename = "meal.png", string contentType = "image/png", bool includeToken = true)
        {
            using var form = new MultipartFormDataContent();
            foreach (var field in new Dictionary<string, string>
            {
                ["Name"] = name,
                ["Description"] = "A recipe upload test",
                ["Category"] = "Món Việt",
                ["Ingredients"] = "Rice",
                ["Instructions"] = "Cook"
            })
            {
                form.Add(new StringContent(field.Value), field.Key);
            }

            if (includeToken)
            {
                string html = await client.GetStringAsync("/Recipe/Create");
                var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
                Assert.True(match.Success);
                form.Add(new StringContent(WebUtility.HtmlDecode(match.Groups[1].Value)), "__RequestVerificationToken");
            }

            if (bytes != null)
            {
                var file = new ByteArrayContent(bytes);
                file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                form.Add(file, "ImageFile", filename);
            }

            return await client.PostAsync(action, form);
        }

        private sealed class UploadFactory : AccountSettingsWebApplicationFactory
        {
            public UploadFactory() : base(false)
            {
                SaveFailure.StoragePath = StoragePath;
            }

            public string StoragePath { get; } = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CookShare-recipe-image-tests-" + Guid.NewGuid().ToString("N")));
            public FailRecipeSave SaveFailure { get; } = new();

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RecipeImages:StoragePath"] = StoragePath
                }));
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureTestServices(services =>
                {
                    var configuredOptions = services.Single(descriptor => descriptor.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    services.Remove(configuredOptions);
                    services.AddScoped(provider =>
                    {
                        var options = (DbContextOptions<ApplicationDbContext>)configuredOptions.ImplementationFactory!(provider);
                        return new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(SaveFailure).Options;
                    });
                });
            }

            public override async ValueTask DisposeAsync()
            {
                try
                {
                    await base.DisposeAsync();
                }
                finally
                {
                    if (Directory.Exists(StoragePath))
                    {
                        string parent = Path.GetDirectoryName(Path.GetFullPath(StoragePath))!;
                        if (!string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), parent, StringComparison.OrdinalIgnoreCase)
                            || !Path.GetFileName(StoragePath).StartsWith("CookShare-recipe-image-tests-", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Refusing to remove a directory outside this test's temporary storage.");
                        }

                        Directory.Delete(StoragePath, recursive: true);
                    }
                }
            }
        }

        private sealed class FailRecipeSave : SaveChangesInterceptor
        {
            public bool Enabled { get; set; }
            public bool SawImageBeforeFailure { get; private set; }
            public string StoragePath { get; set; } = string.Empty;

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
                InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                var changed = eventData.Context!.ChangeTracker.Entries<Recipe>().FirstOrDefault(entry => entry.State is EntityState.Added or EntityState.Modified);
                if (Enabled && changed != null)
                {
                    string? imageReference = changed.Entity.Image;
                    SawImageBeforeFailure = imageReference != null && imageReference.StartsWith("/uploads/recipes/", StringComparison.Ordinal)
                        && File.Exists(Path.Combine(StoragePath, Path.GetFileName(imageReference)));
                    throw new DbUpdateException("Simulated recipe save failure.");
                }

                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }

        private sealed class MisreportedFormFile(byte[] bytes) : IFormFile
        {
            public string ContentType => "image/png";
            public string ContentDisposition => "form-data";
            public IHeaderDictionary Headers { get; } = new HeaderDictionary();
            public long Length => _png.Length;
            public string Name => "ImageFile";
            public string FileName => "misreported.png";
            public Stream OpenReadStream() => new MemoryStream(bytes, writable: false);
            public void CopyTo(Stream target)
            {
                using var source = OpenReadStream();
                source.CopyTo(target);
            }

            public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
            {
                await using var source = OpenReadStream();
                await source.CopyToAsync(target, cancellationToken);
            }
        }
    }
}
