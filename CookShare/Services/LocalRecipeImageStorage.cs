using System.Buffers.Binary;
using CookShare.Options;
using Microsoft.Extensions.Options;

namespace CookShare.Services
{
    public sealed class LocalRecipeImageStorage : IRecipeImageStorage
    {
        public const long MaxFileSize = 5 * 1024 * 1024;
        private const string UrlPrefix = "/uploads/recipes/";
        private readonly string _storagePath;

        public LocalRecipeImageStorage(IWebHostEnvironment environment, IOptions<RecipeImageStorageOptions> options)
        {
            string? configuredPath = options.Value.StoragePath;
            string? azureHome = Environment.GetEnvironmentVariable("HOME");
            bool isAzure = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
            string defaultPath = isAzure && !string.IsNullOrWhiteSpace(azureHome)
                ? Path.Combine(azureHome, "data", "CookShare", "recipe-images")
                : Path.Combine(environment.ContentRootPath, "App_Data", "recipe-images");
            _storagePath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath)
                ? defaultPath
                : Path.Combine(environment.ContentRootPath, configuredPath));

            if (!string.IsNullOrWhiteSpace(environment.WebRootPath))
            {
                string webRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(environment.WebRootPath));
                if (_storagePath.Equals(webRoot, StringComparison.OrdinalIgnoreCase)
                    || _storagePath.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Recipe image storage must be outside the web root.");
                }
            }
        }

        public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(file);
            if (file.Length <= 0)
            {
                throw new RecipeImageUploadException("Vui lòng chọn tệp ảnh có nội dung.");
            }

            if (file.Length > MaxFileSize)
            {
                throw new RecipeImageUploadException("Ảnh không được vượt quá 5 MB.");
            }

            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            {
                throw new RecipeImageUploadException("Chỉ hỗ trợ ảnh JPEG, PNG hoặc WebP.");
            }

            using var contents = new MemoryStream((int)file.Length);
            await using (var source = file.OpenReadStream())
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    if (contents.Length + read > MaxFileSize)
                    {
                        throw new RecipeImageUploadException("Ảnh không được vượt quá 5 MB.");
                    }

                    await contents.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            if (contents.Length != file.Length || !MatchesImage(contents.GetBuffer().AsSpan(0, (int)contents.Length), extension))
            {
                throw new RecipeImageUploadException("Tệp đã chọn không phải ảnh JPEG, PNG hoặc WebP hợp lệ.");
            }

            string fileName = Guid.NewGuid().ToString("N") + (extension == ".jpeg" ? ".jpg" : extension);
            string destination = Path.Combine(_storagePath, fileName);
            bool created = false;
            try
            {
                Directory.CreateDirectory(_storagePath);
                await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    81920, FileOptions.Asynchronous);
                created = true;
                contents.Position = 0;
                await contents.CopyToAsync(output, cancellationToken);
            }
            catch
            {
                if (created)
                {
                    TryDeletePartialFile(destination);
                }

                throw;
            }

            return UrlPrefix + fileName;
        }

        public Task DeleteAsync(string imageReference, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (imageReference.StartsWith(UrlPrefix, StringComparison.Ordinal)
                && TryGetContentType(imageReference[UrlPrefix.Length..], out _))
            {
                File.Delete(Path.Combine(_storagePath, imageReference[UrlPrefix.Length..]));
            }

            return Task.CompletedTask;
        }

        public RecipeImageFile? OpenRead(string fileName)
        {
            if (!TryGetContentType(fileName, out string contentType))
            {
                return null;
            }

            try
            {
                var contents = new FileStream(Path.Combine(_storagePath, fileName), FileMode.Open, FileAccess.Read,
                    FileShare.Read, 81920, FileOptions.Asynchronous);
                return new RecipeImageFile(contents, contentType);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                return null;
            }
        }

        private static bool TryGetContentType(string fileName, out string contentType)
        {
            string extension = Path.GetExtension(fileName);
            contentType = extension switch
            {
                ".jpg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => string.Empty
            };
            return contentType.Length != 0 && fileName.Length == 32 + extension.Length
                && Guid.TryParseExact(fileName[..32], "N", out _);
        }

        private static bool MatchesImage(ReadOnlySpan<byte> contents, string extension)
        {
            if (extension is ".jpg" or ".jpeg")
            {
                return contents.Length >= 5 && contents[0] == 0xff && contents[1] == 0xd8 && contents[2] == 0xff
                    && contents[^2] == 0xff && contents[^1] == 0xd9;
            }

            if (extension == ".png")
            {
                return contents.Length >= 45
                    && contents[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                    && BinaryPrimitives.ReadUInt32BigEndian(contents.Slice(8, 4)) == 13
                    && contents.Slice(12, 4).SequenceEqual("IHDR"u8)
                    && BinaryPrimitives.ReadUInt32BigEndian(contents.Slice(16, 4)) != 0
                    && BinaryPrimitives.ReadUInt32BigEndian(contents.Slice(20, 4)) != 0
                    && contents[^12..].SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 });
            }

            return contents.Length >= 20 && contents[..4].SequenceEqual("RIFF"u8)
                && contents.Slice(8, 4).SequenceEqual("WEBP"u8)
                && BinaryPrimitives.ReadUInt32LittleEndian(contents.Slice(4, 4)) == contents.Length - 8
                && (contents.Slice(12, 4).SequenceEqual("VP8 "u8)
                    || contents.Slice(12, 4).SequenceEqual("VP8L"u8)
                    || contents.Slice(12, 4).SequenceEqual("VP8X"u8));
        }

        private static void TryDeletePartialFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Preserve the original upload failure if the storage volume cannot be cleaned up.
            }
        }
    }
}
