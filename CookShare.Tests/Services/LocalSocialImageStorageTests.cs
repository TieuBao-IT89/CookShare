using System.Buffers.Binary;
using CookShare.Options;
using CookShare.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace CookShare.Tests.Services
{
    public sealed class LocalSocialImageStorageTests : IDisposable
    {
        private static readonly byte[] _png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
        private readonly string _root = Path.Combine(Path.GetTempPath(), "CookShare-social-image-tests", Guid.NewGuid().ToString("N"));

        [Theory]
        [InlineData("png")]
        [InlineData("jpg")]
        [InlineData("webp")]
        public async Task Save_RealImage_UsesGeneratedPrivateFileName_AndCanDelete(string extension)
        {
            var storage = Storage();
            byte[] bytes = ImageBytes(extension);
            string name = await storage.SaveAsync(File(bytes, "../../untrusted." + extension));

            Assert.Matches("^[a-f0-9]{32}\\.(jpg|png|webp)$", name);
            Assert.DoesNotContain("uploads", name);
            var image = Assert.IsType<RecipeImageFile>(storage.OpenRead(name));
            await using (image.Content)
            {
                using var copy = new MemoryStream();
                await image.Content.CopyToAsync(copy);
                Assert.Equal(bytes, copy.ToArray());
            }
            Assert.Null(storage.OpenRead("../" + name));
            await storage.DeleteAsync(name);
            Assert.Null(storage.OpenRead(name));
        }

        [Theory]
        [InlineData(8193, 1)]
        [InlineData(8192, 8192)]
        [InlineData(int.MaxValue, 1)]
        public async Task Save_OversizedDimensionsOrPixelCount_RejectsAndRemovesUploadedFile(int width, int height)
        {
            byte[] bytes = (byte[])_png.Clone();
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16, 4), (uint)width);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20, 4), (uint)height);
            var storage = Storage();

            await Assert.ThrowsAsync<RecipeImageUploadException>(() => storage.SaveAsync(File(bytes, "large.png")));
            Assert.Empty(storage.EnumerateFiles());
        }

        [Theory]
        [InlineData("jpg", "FFD8FF010203FFD9")]
        [InlineData("png", "3C68746D6C3E")]
        public async Task Save_ForgedHeaderOrExtension_RejectsAndLeavesNoFile(string extension, string hex)
        {
            var storage = Storage();
            await Assert.ThrowsAsync<RecipeImageUploadException>(() => storage.SaveAsync(File(Convert.FromHexString(hex), "fake." + extension)));
            Assert.Empty(storage.EnumerateFiles());
        }

        [Fact]
        public void StorageInsideWebRoot_IsRejected()
        {
            var environment = new StorageEnvironment(_root);
            Assert.Throws<InvalidOperationException>(() => new LocalSocialImageStorage(environment,
                Microsoft.Extensions.Options.Options.Create(new SocialImageStorageOptions { StoragePath = Path.Combine(environment.WebRootPath, "uploads") })));
        }

        private LocalSocialImageStorage Storage() => new(new StorageEnvironment(_root),
            Microsoft.Extensions.Options.Options.Create(new SocialImageStorageOptions { StoragePath = Path.Combine(_root, "private") }));

        private static IFormFile File(byte[] bytes, string name) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "ImageFile", name);

        private static byte[] ImageBytes(string extension)
        {
            if (extension == "png")
            {
                return _png;
            }
            if (extension == "jpg")
            {
                return Convert.FromBase64String("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////2wBDAf//////////////////////////////////////////////////////////////////////////////////////wAARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAX/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIQAxAAAAF//8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABBQJ//8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAgBAwEBPwF//8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAgBAgEBPwF//8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQAGPwJ//8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPyF//9oADAMBAAIAAwAAABAf/8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAgBAwEBPxB//8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAgBAgEBPxB//8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxB//9k=");
            }
            string directory = AppContext.BaseDirectory;
            while (!System.IO.File.Exists(Path.Combine(directory, "CookShare.sln")))
            {
                directory = Directory.GetParent(directory)?.FullName ?? throw new InvalidOperationException("Repository not found.");
            }
            return System.IO.File.ReadAllBytes(Path.Combine(directory, "CookShare", "wwwroot", "img", "recipes", "pho.webp"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private sealed class StorageEnvironment(string root) : IWebHostEnvironment
        {
            public string ApplicationName { get; set; } = "CookShare.Tests";
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
            public string EnvironmentName { get; set; } = "Testing";
            public string ContentRootPath { get; set; } = root;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
