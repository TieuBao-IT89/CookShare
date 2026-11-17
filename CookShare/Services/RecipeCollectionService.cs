using CookShare.Data;
using CookShare.Models;
using CookShare.ViewModels;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CookShare.Services
{
    public enum CollectionMutationResult
    {
        Success,
        NotFound
    }

    public sealed class RecipeCollectionService(ApplicationDbContext db)
    {
        public const int PageSize = 24;

        // A profile can only request a public projection. Ownership is checked separately
        // for management pages; callers cannot accidentally opt into private metadata.
        public Task<List<RecipeCollectionSummaryViewModel>> PublicForOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
        {
            return Summaries(db.Set<RecipeCollection>().AsNoTracking()
                .Where(collection => collection.OwnerId == ownerId && collection.IsPublic), null)
                .Take(12).ToListAsync(cancellationToken);
        }

        public async Task<CollectionIndexViewModel> ListOwnedAsync(string ownerId, int page, CancellationToken cancellationToken = default)
        {
            page = NormalizePage(page);
            var rows = await Summaries(db.Set<RecipeCollection>().AsNoTracking().Where(collection => collection.OwnerId == ownerId), null)
                .Skip((page - 1) * PageSize).Take(PageSize + 1).ToListAsync(cancellationToken);
            return new CollectionIndexViewModel { Collections = rows.Take(PageSize).ToArray(), Page = page, HasMore = rows.Count > PageSize };
        }

        public async Task<CollectionDetailsViewModel?> ReadAsync(int id, string? viewerId, int page, CancellationToken cancellationToken = default)
        {
            var collection = await db.Set<RecipeCollection>().AsNoTracking()
                .Where(item => item.Id == id && (item.IsPublic || item.OwnerId == viewerId))
                .Select(item => new CollectionDetailsViewModel
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    OwnerId = item.OwnerId,
                    OwnerName = item.Owner.DisplayName ?? item.Owner.UserName ?? "Người yêu bếp",
                    IsPublic = item.IsPublic,
                    IsOwner = item.OwnerId == viewerId
                }).SingleOrDefaultAsync(cancellationToken);
            if (collection == null) return null;

            page = NormalizePage(page);
            // Join through Recipes to apply its visibility filter before counting/paging.
            var members = from member in db.Set<CollectionRecipe>().AsNoTracking()
                          join recipe in db.Recipes.AsNoTracking() on member.RecipeId equals recipe.Id
                          where member.CollectionId == id
                          orderby member.AddedAt descending, member.RecipeId descending
                          select recipe;
            collection.RecipeCount = await members.CountAsync(cancellationToken);
            var recipes = await members.Include(recipe => recipe.User)
                .Skip((page - 1) * PageSize).Take(PageSize + 1).ToListAsync(cancellationToken);
            collection.Recipes = recipes.Take(PageSize).ToArray();
            collection.Page = page;
            collection.HasMore = recipes.Count > PageSize;
            return collection;
        }

        public async Task<CollectionChooserViewModel?> ChooserAsync(string ownerId, int recipeId, int page, string? returnUrl,
            CancellationToken cancellationToken = default)
        {
            var recipe = await db.Recipes.AsNoTracking().Where(item => item.Id == recipeId)
                .Select(item => new { item.Id, item.Name, item.Image }).SingleOrDefaultAsync(cancellationToken);
            if (recipe == null) return null;
            page = NormalizePage(page);
            var rows = await Summaries(db.Set<RecipeCollection>().AsNoTracking().Where(item => item.OwnerId == ownerId), recipeId)
                .Skip((page - 1) * PageSize).Take(PageSize + 1).ToListAsync(cancellationToken);
            return new CollectionChooserViewModel
            {
                RecipeId = recipe.Id,
                RecipeName = recipe.Name,
                RecipeImage = recipe.Image,
                IsSaved = await db.Favorites.AsNoTracking().AnyAsync(item => item.UserId == ownerId && item.RecipeId == recipeId, cancellationToken),
                Collections = rows.Take(PageSize).ToArray(),
                ReturnUrl = returnUrl,
                Page = page,
                HasMore = rows.Count > PageSize,
                NewCollection = new CollectionInputViewModel { RecipeId = recipeId, ReturnUrl = returnUrl }
            };
        }

        public async Task<RecipeCollection?> CreateAsync(string ownerId, CollectionInputViewModel input, CancellationToken cancellationToken = default)
        {
            if (input.RecipeId.HasValue && !await db.Recipes.AsNoTracking().AnyAsync(recipe => recipe.Id == input.RecipeId.Value, cancellationToken))
                return null;

            var collection = new RecipeCollection
            {
                OwnerId = ownerId,
                Name = input.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
                // Creation always starts private. Public sharing is a deliberate edit.
                IsPublic = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            if (input.RecipeId.HasValue)
                collection.Recipes.Add(new CollectionRecipe { RecipeId = input.RecipeId.Value });
            db.Set<RecipeCollection>().Add(collection);
            // EF inserts the collection and initial membership atomically in one save.
            await db.SaveChangesAsync(cancellationToken);
            return collection;
        }

        public Task<RecipeCollection?> OwnedAsync(string ownerId, int id, CancellationToken cancellationToken = default)
        {
            return db.Set<RecipeCollection>().SingleOrDefaultAsync(collection => collection.Id == id && collection.OwnerId == ownerId, cancellationToken);
        }

        public async Task<bool> EditAsync(string ownerId, int id, CollectionInputViewModel input, CancellationToken cancellationToken = default)
        {
            var collection = await OwnedAsync(ownerId, id, cancellationToken);
            if (collection == null) return false;
            collection.Name = input.Name.Trim();
            collection.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            collection.IsPublic = input.IsPublic;
            collection.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeleteAsync(string ownerId, int id, CancellationToken cancellationToken = default)
        {
            var collection = await OwnedAsync(ownerId, id, cancellationToken);
            if (collection == null) return false;
            db.Set<RecipeCollection>().Remove(collection);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<CollectionMutationResult> SetMembershipAsync(string ownerId, int collectionId, int recipeId, bool added,
            CancellationToken cancellationToken = default)
        {
            if (!await db.Set<RecipeCollection>().AsNoTracking().AnyAsync(collection => collection.Id == collectionId && collection.OwnerId == ownerId, cancellationToken)
                || !await db.Recipes.AsNoTracking().AnyAsync(recipe => recipe.Id == recipeId, cancellationToken))
                return CollectionMutationResult.NotFound;

            var existing = await db.Set<CollectionRecipe>().SingleOrDefaultAsync(member => member.CollectionId == collectionId && member.RecipeId == recipeId, cancellationToken);
            CollectionRecipe? changed = null;
            if (added && existing == null)
            {
                changed = new CollectionRecipe { CollectionId = collectionId, RecipeId = recipeId };
                db.Set<CollectionRecipe>().Add(changed);
            }
            else if (!added && existing != null)
            {
                changed = existing;
                db.Set<CollectionRecipe>().Remove(changed);
            }

            if (changed == null) return CollectionMutationResult.Success;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (added && exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_CollectionRecipes" })
            {
                db.Entry(changed).State = EntityState.Detached;
            }
            catch (DbUpdateConcurrencyException) when (!added)
            {
                db.Entry(changed).State = EntityState.Detached;
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
            {
                db.Entry(changed).State = EntityState.Detached;
                if (!await db.Set<RecipeCollection>().AsNoTracking().AnyAsync(collection => collection.Id == collectionId && collection.OwnerId == ownerId, cancellationToken)
                    || !await db.Recipes.AsNoTracking().AnyAsync(recipe => recipe.Id == recipeId, cancellationToken))
                    return CollectionMutationResult.NotFound;
                throw;
            }
            return CollectionMutationResult.Success;
        }

        private static IQueryable<RecipeCollectionSummaryViewModel> Summaries(IQueryable<RecipeCollection> source, int? recipeId)
        {
            return source.OrderByDescending(collection => collection.UpdatedAt).ThenByDescending(collection => collection.Id)
                .Select(collection => new RecipeCollectionSummaryViewModel
                {
                    Id = collection.Id,
                    Name = collection.Name,
                    Description = collection.Description,
                    IsPublic = collection.IsPublic,
                    RecipeCount = collection.Recipes.Count(member => !member.Recipe.IsHidden),
                    CoverImage = collection.Recipes.Where(member => !member.Recipe.IsHidden && member.Recipe.Image != null && member.Recipe.Image != "")
                        .OrderByDescending(member => member.AddedAt).ThenByDescending(member => member.RecipeId)
                        .Select(member => member.Recipe.Image).FirstOrDefault(),
                    ContainsRecipe = recipeId.HasValue && collection.Recipes.Any(member => member.RecipeId == recipeId.Value)
                });
        }

        private static int NormalizePage(int page)
        {
            return Math.Clamp(page, 1, 10000);
        }
    }
}
