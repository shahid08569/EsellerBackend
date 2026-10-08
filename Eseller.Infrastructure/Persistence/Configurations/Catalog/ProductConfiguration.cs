using Eseller.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Catalog;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(250);
        builder.Property(p => p.Slug).IsRequired().HasMaxLength(300);
        builder.Property(p => p.Description).HasMaxLength(4000);
        builder.Property(p => p.BasePrice).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.IsFeatured).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.IsHotSelling).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.IsApproved).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.IsStorefrontLive).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.ViewCount).IsRequired().HasDefaultValue(0);
        builder.Property(p => p.AvgRating).HasPrecision(3, 2);
        builder.Property(p => p.ReviewCount).IsRequired().HasDefaultValue(0);
        builder.Property(p => p.SeoTitle).HasMaxLength(200);
        builder.Property(p => p.SeoDescription).HasMaxLength(500);
        builder.Property(p => p.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();
        

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(p => p.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Brand>()
            .WithMany()
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(p => p.SourceProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.RejectionReason)
            .HasMaxLength(500);

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => p.ShopId);
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.BrandId);
        builder.HasIndex(p => p.SourceProductId);
        builder.HasIndex(p => new { p.ShopId, p.SourceProductId });
        builder.HasIndex(p => p.IsActive);
        builder.HasIndex(p => p.IsFeatured);
        builder.HasIndex(p => p.IsHotSelling);
        builder.HasIndex(p => p.IsApproved);
        builder.HasIndex(p => p.IsStorefrontLive);

        builder.HasQueryFilter(s => s.IsActive);
    }
}