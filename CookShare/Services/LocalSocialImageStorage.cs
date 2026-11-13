using System.Buffers.Binary;
using CookShare.Options;
using Microsoft.Extensions.Options;

namespace CookShare.Services
{
    public sealed class LocalSocialImageStorage : ISocialImageStorage
    {
        public const int MaxDimension = 8192;
        public const long MaxPixels = 40_000_000;
        private const string RecipePrefix = "/uploads/recipes/";
        private readonly LocalRecipeImageStorage _inner;
        private readonly string _storagePath;

        public LocalSocialImageStorage(IWebHostEnvironment environment, IOptions<SocialImageStorageOptions> options)
        {
            string? azureHome = Environment.GetEnvironmentVariable("HOME");
            bool isAzure = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
            string defaultPath = isAzure && !string.IsNullOrWhiteSpace(azureHome)
                ? Path.Combine(azureHome, "data", "CookShare", "cooked-post-images")
                : Path.Combine(environment.ContentRootPath, "App_Data", "cooked-post-images");
            _storagePath = Path.GetFullPath(string.IsNullOrWhiteSpace(options.Value.StoragePath)
                ? defaultPath : Path.Combine(environment.ContentRootPath, options.Value.StoragePath));
            _inner = new LocalRecipeImageStorage(environment,
                Microsoft.Extensions.Options.Options.Create(new RecipeImageStorageOptions { StoragePath = _storagePath }));
        }

        public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            // Reuse the existing bounded copy, real signature checks and generated filenames.
            string reference = await _inner.SaveAsync(file, cancellationToken);
            string fileName = reference[RecipePrefix.Length..];
            try
            {
                var image = _inner.OpenRead(fileName) ?? throw new IOException("The uploaded image could not be read.");
                await using (image.Content)
                {
                    using var data = new MemoryStream();
                    await image.Content.CopyToAsync(data, cancellationToken);
                    if (!TryReadDimensions(data.GetBuffer().AsSpan(0, (int)data.Length), Path.GetExtension(fileName), out int width, out int height)
                        || width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension || (long)width * height > MaxPixels)
                    {
                        throw new RecipeImageUploadException("Ảnh phải có kích thước hợp lệ, mỗi chiều tối đa 8192 px và tổng tối đa 40 triệu điểm ảnh.");
                    }
                }
                return fileName;
            }
            catch
            {
                await _inner.DeleteAsync(reference, CancellationToken.None);
                throw;
            }
        }

        public RecipeImageFile? OpenRead(string fileName) => _inner.OpenRead(fileName);

        public Task DeleteAsync(string fileName, CancellationToken cancellationToken = default)
            => _inner.DeleteAsync(RecipePrefix + fileName, cancellationToken);

        public IEnumerable<SocialImageEntry> EnumerateFiles()
        {
            if (!Directory.Exists(_storagePath))
            {
                return Array.Empty<SocialImageEntry>();
            }
            return Directory.EnumerateFiles(_storagePath).Select(path => new SocialImageEntry(Path.GetFileName(path), File.GetLastWriteTimeUtc(path)));
        }

        private static bool TryReadDimensions(ReadOnlySpan<byte> bytes, string extension, out int width, out int height)
        {
            width = height = 0;
            if (extension == ".png" && bytes.Length >= 33)
            {
                uint rawWidth = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4));
                uint rawHeight = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4));
                if (rawWidth > int.MaxValue || rawHeight > int.MaxValue)
                {
                    return false;
                }
                width = (int)rawWidth;
                height = (int)rawHeight;
                return true;
            }
            if (extension == ".jpg")
            {
                int offset = 2;
                while (offset + 4 <= bytes.Length)
                {
                    if (bytes[offset++] != 0xff)
                    {
                        return false;
                    }
                    while (offset < bytes.Length && bytes[offset] == 0xff)
                    {
                        offset++;
                    }
                    if (offset >= bytes.Length)
                    {
                        return false;
                    }
                    byte marker = bytes[offset++];
                    if (marker is 0xd9 or 0xda)
                    {
                        return false;
                    }
                    if (marker == 0x01 || marker is >= 0xd0 and <= 0xd8)
                    {
                        continue;
                    }
                    if (offset + 2 > bytes.Length)
                    {
                        return false;
                    }
                    int length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
                    if (length < 2 || offset + length > bytes.Length)
                    {
                        return false;
                    }
                    bool startOfFrame = marker is >= 0xc0 and <= 0xcf && marker is not (0xc4 or 0xc8 or 0xcc);
                    if (startOfFrame && length >= 8)
                    {
                        height = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 3, 2));
                        width = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 5, 2));
                        return true;
                    }
                    offset += length;
                }
                return false;
            }
            if (extension == ".webp" && bytes.Length >= 30)
            {
                if (bytes.Slice(12, 4).SequenceEqual("VP8X"u8))
                {
                    width = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16);
                    height = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16);
                    return true;
                }
                if (bytes.Slice(12, 4).SequenceEqual("VP8L"u8) && bytes[20] == 0x2f)
                {
                    width = 1 + bytes[21] + ((bytes[22] & 0x3f) << 8);
                    height = 1 + (bytes[22] >> 6) + (bytes[23] << 2) + ((bytes[24] & 0x0f) << 10);
                    return true;
                }
                if (bytes.Slice(12, 4).SequenceEqual("VP8 "u8) && bytes.Slice(23, 3).SequenceEqual(new byte[] { 0x9d, 0x01, 0x2a }))
                {
                    width = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(26, 2)) & 0x3fff;
                    height = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(28, 2)) & 0x3fff;
                    return true;
                }
            }
            return false;
        }
    }
}
