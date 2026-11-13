namespace CookShare.Services
{
    public interface ISocialImageStorage
    {
        Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default);
        RecipeImageFile? OpenRead(string fileName);
        Task DeleteAsync(string fileName, CancellationToken cancellationToken = default);
        IEnumerable<SocialImageEntry> EnumerateFiles();
    }

    public sealed record SocialImageEntry(string FileName, DateTime LastWriteUtc);
}
