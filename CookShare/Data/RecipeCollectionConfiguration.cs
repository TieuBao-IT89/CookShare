using CookShare.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookShare.Data
{
    public sealed class RecipeCollectionConfiguration : IEntityTypeConfiguration<RecipeCollection>
    {
        public void Configure(EntityTypeBuilder<RecipeCollection> builder)
        {
            builder.HasKey(collection => collection.Id);
            builder.Property(collection => collection.Name).IsRequired().HasMaxLength(80);
            builder.Property(collection => collection.Description).HasMaxLength(300);
            builder.Property(collection => collection.OwnerId).IsRequired().HasMaxLength(450);
            builder.Property(collection => collection.IsPublic).HasDefaultValue(false);
            builder.HasIndex(collection => new { collection.OwnerId, collection.UpdatedAt, collection.Id });
            builder.HasOne(collection => collection.Owner).WithMany()
                .HasForeignKey(collection => collection.OwnerId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class CollectionRecipeConfiguration : IEntityTypeConfiguration<CollectionRecipe>
    {
        public void Configure(EntityTypeBuilder<CollectionRecipe> builder)
        {
            builder.HasKey(member => new { member.CollectionId, member.RecipeId }).HasName("PK_CollectionRecipes");
            builder.HasIndex(member => new { member.CollectionId, member.AddedAt, member.RecipeId });
            builder.HasOne(member => member.Collection).WithMany(collection => collection.Recipes)
                .HasForeignKey(member => member.CollectionId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(member => member.Recipe).WithMany()
                .HasForeignKey(member => member.RecipeId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
