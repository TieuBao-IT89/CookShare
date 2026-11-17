using CookShare.Data;
using CookShare.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CookShare.Services
{
    public sealed class CookedPostService(
        ApplicationDbContext db,
        ISocialImageStorage storage,
        NotificationService notifications,
        ILogger<CookedPostService> logger)
    {
        private static readonly Action<ILogger, string, Exception?> _logCleanupFailure =
            LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2031, "CookedPostMediaCleanupFailure"),
                "Cooked-post media cleanup failed for {FileName}; retry using the orphan cleanup command.");

        public async Task<int> CreateAsync(string userId, int recipeId, string? note, Guid submissionKey,
            IFormFile image, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);
            ValidateNote(note);
            if (submissionKey == Guid.Empty)
            {
                throw new ArgumentException("A submission key is required.", nameof(submissionKey));
            }
            int? existingId = await FindSubmissionAsync(userId, submissionKey, cancellationToken);
            if (existingId != null)
            {
                return existingId.Value;
            }
            var recipe = await db.Recipes.SingleOrDefaultAsync(item => item.Id == recipeId && !item.IsHidden, cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy công thức.");
            await using var transaction = await BeginTransactionAsync(cancellationToken);
            string fileName = await storage.SaveAsync(image, cancellationToken);
            bool committed = false;
            var post = new CookedRecipePost
            {
                Recipe = recipe,
                RecipeId = recipe.Id,
                UserId = userId,
                Note = note?.Trim() ?? string.Empty,
                ImageFileName = fileName,
                SubmissionKey = submissionKey
            };
            try
            {
                db.Set<CookedRecipePost>().Add(post);
                await db.SaveChangesAsync(cancellationToken);
                await notifications.NotifyCookedRecipePostAsync(post);
                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
                committed = true;
                return post.Id;
            }
            catch (DbUpdateException exception)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                db.ChangeTracker.Clear();
                // The unique key also resolves two simultaneous submissions after rollback.
                int? winner = exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "IX_CookedRecipePosts_UserId_SubmissionKey"
                } ? await FindSubmissionAsync(userId, submissionKey, cancellationToken) : null;
                if (winner != null)
                {
                    return winner.Value;
                }
                throw;
            }
            finally
            {
                if (!committed)
                {
                    await TryDeleteAsync(fileName);
                }
            }
        }

        public async Task UpdateAsync(int id, string userId, string? note, IFormFile? replacement,
            CancellationToken cancellationToken = default)
        {
            ValidateNote(note);
            var post = await OwnedPostAsync(id, userId, cancellationToken);
            string oldFileName = post.ImageFileName;
            string? newFileName = replacement == null ? null : await storage.SaveAsync(replacement, cancellationToken);
            bool committed = false;
            try
            {
                post.Note = note?.Trim() ?? string.Empty;
                post.UpdatedAt = DateTime.UtcNow;
                if (newFileName != null)
                {
                    post.ImageFileName = newFileName;
                }
                await db.SaveChangesAsync(cancellationToken);
                committed = true;
            }
            finally
            {
                if (newFileName != null)
                {
                    await TryDeleteAsync(committed ? oldFileName : newFileName);
                }
            }
        }

        public async Task DeleteAsync(int id, string userId, CancellationToken cancellationToken = default)
        {
            // Owners may remove their own hidden post. They cannot read its media through this path.
            var post = await db.Set<CookedRecipePost>().IgnoreQueryFilters().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (post == null)
            {
                return;
            }
            if (post.UserId != userId)
            {
                throw new UnauthorizedAccessException();
            }
            db.Set<CookedRecipePost>().Remove(post);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A simultaneous owner request may have already removed the row.
                if (await db.Set<CookedRecipePost>().IgnoreQueryFilters().AsNoTracking().AnyAsync(item => item.Id == id, cancellationToken))
                {
                    throw;
                }
                db.Entry(post).State = EntityState.Detached;
            }
            await TryDeleteAsync(post.ImageFileName);
        }

        public async Task<int> CleanOrphanedImagesAsync(CancellationToken cancellationToken = default)
        {
            int removed = 0;
            DateTime cutoff = DateTime.UtcNow.AddDays(-1);
            foreach (var batch in storage.EnumerateFiles().Where(file => file.LastWriteUtc < cutoff).Chunk(200))
            {
                string[] names = batch.Select(file => file.FileName).ToArray();
                var referenced = (await db.Set<CookedRecipePost>().IgnoreQueryFilters()
                    .Where(post => names.Contains(post.ImageFileName)).Select(post => post.ImageFileName)
                    .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
                foreach (var file in batch.Where(file => !referenced.Contains(file.FileName)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await storage.DeleteAsync(file.FileName, cancellationToken);
                    removed++;
                }
            }
            return removed;
        }

        private Task<int?> FindSubmissionAsync(string userId, Guid key, CancellationToken cancellationToken)
            => db.Set<CookedRecipePost>().IgnoreQueryFilters().Where(post => post.UserId == userId && post.SubmissionKey == key)
                .Select(post => (int?)post.Id).SingleOrDefaultAsync(cancellationToken);

        private async Task<CookedRecipePost> OwnedPostAsync(int id, string userId, CancellationToken cancellationToken)
        {
            var post = await db.Set<CookedRecipePost>().SingleOrDefaultAsync(item => item.Id == id && !item.IsHidden && !item.Recipe.IsHidden, cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy bài đã nấu thử.");
            if (post.UserId != userId)
            {
                throw new UnauthorizedAccessException();
            }
            return post;
        }

        private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
            => db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

        private async Task TryDeleteAsync(string fileName)
        {
            try
            {
                await storage.DeleteAsync(fileName, CancellationToken.None);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logCleanupFailure(logger, fileName, exception);
            }
        }

        private static void ValidateNote(string? note)
        {
            if (note?.Length > 1000)
            {
                throw new ArgumentException("Ghi chú không được vượt quá 1000 ký tự.", nameof(note));
            }
        }
    }
}
