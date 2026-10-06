using Eseller.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Catalog;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(200);
        builder.Property(c => c.ImageUrl).HasMaxLength(500);
        builder.Property(c => c.SeoTitle).HasMaxLength(200);
        builder.Property(c => c.SeoDescription).HasMaxLength(500);
        builder.Property(c => c.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();
        builder.Property(c => c.IsFeaturedOnHomepage)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.HomepageDisplayOrder)
            .IsRequired()
            .HasDefaultValue(0);
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.Slug).IsUnique();
        builder.HasIndex(c => c.ParentCategoryId);
        builder.HasIndex(c => c.IsActive);

        builder.HasIndex(c => new { c.IsFeaturedOnHomepage, c.HomepageDisplayOrder });

        // Soft-delete only — IsActive controls nav visibility, not product lookups
        builder.HasQueryFilter(c => c.DeletedAt == null);
    }
}