
using CookShare.Models;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Recipe> Recipes => Set<Recipe>();

        public DbSet<RecipeLike> RecipeLikes
            => Set<RecipeLike>();

        public DbSet<RecipeComment> RecipeComments
            => Set<RecipeComment>();

        public DbSet<Favorite> Favorites
            => Set<Favorite>();

        public DbSet<Follow> Follows
            => Set<Follow>();

        public DbSet<RecipeRating> RecipeRatings
            => Set<RecipeRating>();

        public DbSet<Notification> Notifications
            => Set<Notification>();

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Recipe
            modelBuilder.Entity<Recipe>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(r => r.Category)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(r => r.UserId)
                    .HasMaxLength(450);

                entity.HasOne(r => r.User)
                    .WithMany(u => u.Recipes)
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ApplicationUser
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(u => u.DisplayName)
                    .HasMaxLength(100);

                entity.Property(u => u.AvatarUrl)
                    .HasMaxLength(500);
            });

            // Notification
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.RecipientId)
                    .IsRequired();

                entity.Property(x => x.ActorId)
                    .IsRequired();

                entity.Property(x => x.Type)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(x => x.Message)
                    .HasMaxLength(500);

                entity.Property(x => x.EventKey)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.HasIndex(x => x.EventKey)
                    .IsUnique();

                entity.HasIndex(x => new
                {
                    x.RecipientId,
                    x.IsRead,
                    x.CreatedAt
                });

                entity.HasOne(x => x.Recipient)
                    .WithMany()
                    .HasForeignKey(x => x.RecipientId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Actor)
                    .WithMany()
                    .HasForeignKey(x => x.ActorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Recipe>()
                    .WithMany()
                    .HasForeignKey(x => x.RecipeId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne<RecipeComment>()
                    .WithMany()
                    .HasForeignKey(x => x.CommentId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // RecipeLike
            modelBuilder.Entity<RecipeLike>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new
                {
                    x.UserId,
                    x.RecipeId
                }).IsUnique();

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Recipe)
                    .WithMany(r => r.Likes)
                    .HasForeignKey(x => x.RecipeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // RecipeComment
            modelBuilder.Entity<RecipeComment>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Content)
                    .IsRequired()
                    .HasMaxLength(1000);

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Recipe)
                    .WithMany(r => r.Comments)
                    .HasForeignKey(x => x.RecipeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Favorite
            modelBuilder.Entity<Favorite>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new
                {
                    x.UserId,
                    x.RecipeId
                }).IsUnique();

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Recipe)
                    .WithMany(r => r.Favorites)
                    .HasForeignKey(x => x.RecipeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Follow
            modelBuilder.Entity<Follow>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new
                {
                    x.FollowerId,
                    x.FollowingId
                }).IsUnique();

                entity.HasOne(x => x.Follower)
                    .WithMany()
                    .HasForeignKey(x => x.FollowerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Following)
                    .WithMany()
                    .HasForeignKey(x => x.FollowingId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.ToTable(t =>
                    t.HasCheckConstraint(
                        "CK_Follows_NoSelfFollow",
                        "\"FollowerId\" <> \"FollowingId\""));
            });

            // RecipeRating
            modelBuilder.Entity<RecipeRating>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new
                {
                    x.UserId,
                    x.RecipeId
                }).IsUnique();

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Recipe)
                    .WithMany(r => r.Ratings)
                    .HasForeignKey(x => x.RecipeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.ToTable(t =>
                    t.HasCheckConstraint(
                        "CK_RecipeRatings_Stars",
                        "\"Stars\" >= 1 AND \"Stars\" <= 5"));
            });
        }
    }
}
