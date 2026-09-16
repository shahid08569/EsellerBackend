using Eseller.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Catalog;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants");
        builder.HasKey(pv => pv.Id);

        builder.Property(pv => pv.ImageUrl).HasMaxLength(500);
        builder.Property(pv => pv.StockQty).IsRequired().HasDefaultValue(0);
        builder.Property(pv => pv.LowStockThreshold).IsRequired().HasDefaultValue(5);
        builder.Property(pv => pv.Price).HasPrecision(18, 2).IsRequired();
        builder.Property(pv => pv.Sku).IsRequired().HasMaxLength(100);
        builder.Property(pv => pv.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(pv => pv.CreatedAt).IsRequired();
        builder.Property(pv => pv.UpdatedAt).IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(pv => pv.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(pv => pv.Sku).IsUnique();
        builder.HasIndex(pv => pv.ProductId);
        builder.HasIndex(pv => pv.IsActive);

        builder.HasQueryFilter(s => s.IsActive);
    }
}