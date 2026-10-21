
using CookShare.Models;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options
        ) : base(options)
        {
        }

        public DbSet<Recipe> Recipes => Set<Recipe>();

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Recipe>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(r => r.Category)
                    .IsRequired()
                    .HasMaxLength(100);
            });
        }
    }
}
