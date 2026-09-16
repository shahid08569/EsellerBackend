using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Promotions;

public class FlashSaleProductConfiguration : IEntityTypeConfiguration<FlashSaleProduct>
{
    public void Configure(EntityTypeBuilder<FlashSaleProduct> builder)
    {
        builder.ToTable("FlashSaleProducts");
        builder.HasKey(fsp => fsp.Id);

        builder.Property(fsp => fsp.DiscountType).HasConversion<int>().IsRequired();
        builder.Property(fsp => fsp.DiscountValue).HasPrecision(18, 2).IsRequired();

        builder.HasOne<FlashSale>()
            .WithMany()
            .HasForeignKey(fsp => fsp.FlashSaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(fsp => fsp.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(fsp => new { fsp.FlashSaleId, fsp.ProductId }).IsUnique();
    }
}