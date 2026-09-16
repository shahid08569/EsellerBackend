using Eseller.Domain.Entities.Content;
using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Content;

public class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        builder.ToTable("BlogPosts");
        builder.HasKey(bp => bp.Id);

        builder.Property(bp => bp.Title).IsRequired().HasMaxLength(300);
        builder.Property(bp => bp.Slug).IsRequired().HasMaxLength(350);
        builder.Property(bp => bp.Content).IsRequired();
        builder.Property(bp => bp.CoverImageUrl).HasMaxLength(500);
        builder.Property(bp => bp.IsPublished).IsRequired().HasDefaultValue(false);
        builder.Property(bp => bp.SeoTitle).HasMaxLength(200);
        builder.Property(bp => bp.SeoDescription).HasMaxLength(500);
        builder.Property(bp => bp.CreatedAt).IsRequired();
        builder.Property(bp => bp.UpdatedAt).IsRequired();

        builder.HasOne<BlogCategory>()
            .WithMany()
            .HasForeignKey(bp => bp.BlogCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(bp => bp.CreatedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(bp => bp.Slug).IsUnique();
        builder.HasIndex(bp => bp.BlogCategoryId);
        builder.HasIndex(bp => bp.IsPublished);
    }
}