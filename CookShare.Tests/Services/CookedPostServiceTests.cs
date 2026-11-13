using CookShare.Data;
using CookShare.Models;
using CookShare.Models.Identity;
using CookShare.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace CookShare.Tests.Services
{
    public sealed class CookedPostServiceTests
    {
        [Fact]
        public async Task Create_CommitsPostAndOneNotification_AndDuplicateSubmissionDoesNotUploadAgain()
        {
            await using var fixture = await SocialTestDatabase.CreateAsync();
            var service = fixture.Service();
            Guid key = Guid.NewGuid();
            int id = await service.CreateAsync("cook", fixture.RecipeId, "  Thành công!  ", key, SocialTestDatabase.Image());
            int retry = await service.CreateAsync("cook", fixture.RecipeId, "ignored", key, SocialTestDatabase.Image());

            Assert.Equal(id, retry);
            Assert.Equal(1, fixture.Storage.SaveCount);
            fixture.Db.ChangeTracker.Clear();
            var post = Assert.Single(await fixture.Db.Set<CookedRecipePost>().ToListAsync());
            Assert.Equal("Thành công!", post.Note);
            var notification = Assert.Single(await fixture.Db.Notifications.ToListAsync());
            Assert.Equal("owner", notification.RecipientId);
            Assert.Equal("cook", notification.ActorId);
            Assert.Equal(id, notification.CookedRecipePostId);
        }

        [Fact]
        public async Task Create_OwnRecipeDoesNotSelfNotify()
        {
            await using var fixture = await SocialTestDatabase.CreateAsync();
            await fixture.Service().CreateAsync("owner", fixture.RecipeId, "", Guid.NewGuid(), SocialTestDatabase.Image());
            Assert.Empty(await fixture.Db.Notifications.ToListAsync());
        }

        [Fact]
        public async Task NotificationFailure_RollsBackPostAndCleansNewFile_ThenRetrySucceeds()
        {
            var interceptor = new SocialSaveFailureInterceptor { RejectNotification = true };
            await using var fixture = await SocialTestDatabase.CreateAsync(interceptor);
            Guid key = Guid.NewGuid();

            await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Service().CreateAsync(
                "cook", fixture.RecipeId, "", key, SocialTestDatabase.Image()));

            fixture.Db.ChangeTracker.Clear();
            Assert.Empty(await fixture.Db.Set<CookedRecipePost>().IgnoreQueryFilters().ToListAsync());
            Assert.Empty(await fixture.Db.Notifications.ToListAsync());
            Assert.Empty(fixture.Storage.Files);
            interceptor.RejectNotification = false;
            await fixture.Service().CreateAsync("cook", fixture.RecipeId, "", key, SocialTestDatabase.Image());
            Assert.Single(await fixture.Db.Set<CookedRecipePost>().ToListAsync());
            Assert.Single(await fixture.Db.Notifications.ToListAsync());
            Assert.Single(fixture.Storage.Files);
        }

        [Fact]
        public async Task ReplacementFailure_PreservesCommittedImage_AndCleansReplacement()
        {
            var interceptor = new SocialSaveFailureInterceptor();
            await using var fixture = await SocialTestDatabase.CreateAsync(interceptor);
            int id = await fixture.Service().CreateAsync("cook", fixture.RecipeId, "before", Guid.NewGuid(), SocialTestDatabase.Image());
            string oldFile = Assert.Single(fixture.Storage.Files.Keys);
            interceptor.RejectPost = true;

            await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Service().UpdateAsync(id, "cook", "after", SocialTestDatabase.Image()));

            fixture.Db.ChangeTracker.Clear();
            var post = await fixture.Db.Set<CookedRecipePost>().SingleAsync();
            Assert.Equal(oldFile, post.ImageFileName);
            Assert.Equal("before", post.Note);
            Assert.Equal(oldFile, Assert.Single(fixture.Storage.Files.Keys));
        }

        [Fact]
        public async Task ReplacementAndDelete_RemoveFilesOnlyAfterDatabaseCommit()
        {
            await using var fixture = await SocialTestDatabase.CreateAsync();
            int id = await fixture.Service().CreateAsync("cook", fixture.RecipeId, "before", Guid.NewGuid(), SocialTestDatabase.Image());
            string oldFile = Assert.Single(fixture.Storage.Files.Keys);
            fixture.Storage.BeforeDelete = async file => Assert.False(await fixture.Db.Set<CookedRecipePost>().IgnoreQueryFilters()
                .AsNoTracking().AnyAsync(post => post.ImageFileName == file));

            await fixture.Service().UpdateAsync(id, "cook", "after", SocialTestDatabase.Image());
            Assert.DoesNotContain(oldFile, fixture.Storage.Files.Keys);
            Assert.Single(fixture.Storage.Files);
            await fixture.Service().DeleteAsync(id, "cook");
            await fixture.Service().DeleteAsync(id, "cook");
            Assert.Empty(fixture.Storage.Files);
            Assert.Empty(await fixture.Db.Set<CookedRecipePost>().IgnoreQueryFilters().ToListAsync());
            Assert.Null((await fixture.Db.Notifications.SingleAsync()).CookedRecipePostId);
        }

        [Fact]
        public async Task ForeignOwner_CannotEditOrDelete_AndHiddenParentCannotReceivePost()
        {
            await using var fixture = await SocialTestDatabase.CreateAsync();
            int id = await fixture.Service().CreateAsync("cook", fixture.RecipeId, "", Guid.NewGuid(), SocialTestDatabase.Image());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service().UpdateAsync(id, "owner", "tampered", SocialTestDatabase.Image()));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service().DeleteAsync(id, "owner"));
            var recipe = await fixture.Db.Recipes.SingleAsync();
            recipe.IsHidden = true;
            await fixture.Db.SaveChangesAsync();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service().CreateAsync(
                "cook", fixture.RecipeId, "", Guid.NewGuid(), SocialTestDatabase.Image()));
            Assert.Equal(1, fixture.Storage.SaveCount);
        }

        [Fact]
        public async Task OrphanCleanup_KeepsHiddenReferencesAndRecentUploads_AndRemovesOldOrphans()
        {
            await using var fixture = await SocialTestDatabase.CreateAsync();
            int id = await fixture.Service().CreateAsync("cook", fixture.RecipeId, "", Guid.NewGuid(), SocialTestDatabase.Image());
            var post = await fixture.Db.Set<CookedRecipePost>().SingleAsync(item => item.Id == id);
            post.IsHidden = true;
            await fixture.Db.SaveChangesAsync();
            fixture.Storage.Files[post.ImageFileName] = DateTime.UtcNow.AddDays(-2);
            fixture.Storage.Files["old-orphan.png"] = DateTime.UtcNow.AddDays(-2);
            fixture.Storage.Files["in-flight.png"] = DateTime.UtcNow;

            Assert.Equal(1, await fixture.Service().CleanOrphanedImagesAsync());
            Assert.Contains(post.ImageFileName, fixture.Storage.Files.Keys);
            Assert.Contains("in-flight.png", fixture.Storage.Files.Keys);
            Assert.DoesNotContain("old-orphan.png", fixture.Storage.Files.Keys);
        }

        [Fact]
        public async Task ConcurrentOwnerDelete_AlreadyRemovedRowIsSuccessful_AndMediaIsCleaned()
        {
            var interceptor = new ConcurrentOwnerDeleteInterceptor();
            await using var fixture = await SocialTestDatabase.CreateAsync(interceptor);
            int id = await fixture.Service().CreateAsync("cook", fixture.RecipeId, "", Guid.NewGuid(), SocialTestDatabase.Image());
            interceptor.Enabled = true;

            await fixture.Service().DeleteAsync(id, "cook");

            Assert.True(interceptor.SimulatedCompetingDelete);
            Assert.Empty(fixture.Storage.Files);
            Assert.Empty(await fixture.Db.Set<CookedRecipePost>().IgnoreQueryFilters().ToListAsync());
        }

        private sealed class ConcurrentOwnerDeleteInterceptor : SaveChangesInterceptor
        {
            public bool Enabled { get; set; }
            public bool SimulatedCompetingDelete { get; private set; }
            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
                InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                var db = eventData.Context!;
                var deletion = db.ChangeTracker.Entries<CookedRecipePost>().SingleOrDefault(entry => entry.State == EntityState.Deleted);
                if (Enabled && deletion != null)
                {
                    // The competing request commits after this request's read and before its DELETE.
                    await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"CookedRecipePosts\" WHERE \"Id\" = {deletion.Entity.Id}", cancellationToken);
                    SimulatedCompetingDelete = true;
                }
                return result;
            }
        }
    }

    internal sealed class SocialTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Db { get; }
        public RecordingSocialImageStorage Storage { get; } = new();
        public int RecipeId { get; private set; }

        private SocialTestDatabase(SqliteConnection connection, ApplicationDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public static async Task<SocialTestDatabase> CreateAsync(SaveChangesInterceptor? interceptor = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection);
            if (interceptor != null)
            {
                options.AddInterceptors(interceptor);
            }
            var db = new ApplicationDbContext(options.Options);
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(new ApplicationUser { Id = "owner", UserName = "owner" },
                new ApplicationUser { Id = "cook", UserName = "cook" });
            var recipe = new Recipe
            {
                Name = "Canh chua",
                Category = "Món Việt",
                Description = "Món nhà nấu",
                Ingredients = "Cà chua",
                Instructions = "Nấu chín",
                UserId = "owner"
            };
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            return new SocialTestDatabase(connection, db) { RecipeId = recipe.Id };
        }

        public CookedPostService Service() => new(Db, Storage, new NotificationService(Db), NullLogger<CookedPostService>.Instance);

        public static IFormFile Image() => new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "ImageFile", "meal.png");

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    internal sealed class RecordingSocialImageStorage : ISocialImageStorage
    {
        public Dictionary<string, DateTime> Files { get; } = new(StringComparer.Ordinal);
        public int SaveCount { get; private set; }
        public Func<string, Task>? BeforeDelete { get; set; }

        public Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            string name = Guid.NewGuid().ToString("N") + ".png";
            Files[name] = DateTime.UtcNow;
            return Task.FromResult(name);
        }

        public RecipeImageFile? OpenRead(string fileName) => Files.ContainsKey(fileName)
            ? new RecipeImageFile(new MemoryStream(new byte[] { 1, 2, 3 }), "image/png") : null;

        public async Task DeleteAsync(string fileName, CancellationToken cancellationToken = default)
        {
            if (BeforeDelete != null)
            {
                await BeforeDelete(fileName);
            }
            Files.Remove(fileName);
        }

        public IEnumerable<SocialImageEntry> EnumerateFiles() => Files.Select(item => new SocialImageEntry(item.Key, item.Value)).ToArray();
    }

    internal sealed class SocialSaveFailureInterceptor : SaveChangesInterceptor
    {
        public bool RejectNotification { get; set; }
        public bool RejectPost { get; set; }
        public bool RejectAudit { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var db = eventData.Context!;
            if ((RejectNotification && db.ChangeTracker.Entries<Notification>().Any(entry => entry.State == EntityState.Added))
                || (RejectPost && db.ChangeTracker.Entries<CookedRecipePost>().Any(entry => entry.State == EntityState.Modified))
                || (RejectAudit && db.ChangeTracker.Entries<ModerationAction>().Any(entry => entry.State == EntityState.Added)))
            {
                throw new DbUpdateException("Simulated social content save failure.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
