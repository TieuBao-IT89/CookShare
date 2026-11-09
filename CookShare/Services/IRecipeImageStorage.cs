namespace CookShare.Services
{
    public interface IRecipeImageStorage
    {
        Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default);

        Task DeleteAsync(string imageReference, CancellationToken cancellationToken = default);

        RecipeImageFile? OpenRead(string fileName);
    }

    public sealed record RecipeImageFile(Stream Content, string ContentType);

    public sealed class RecipeImageUploadException : Exception
    {
        public RecipeImageUploadException()
        {
        }

        public RecipeImageUploadException(string message) : base(message)
        {
        }

        public RecipeImageUploadException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
