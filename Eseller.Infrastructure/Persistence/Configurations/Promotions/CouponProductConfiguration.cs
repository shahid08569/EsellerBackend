using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Promotions;

public class CouponProductConfiguration : IEntityTypeConfiguration<CouponProduct>
{
    public void Configure(EntityTypeBuilder<CouponProduct> builder)
    {
        builder.ToTable("CouponProducts");
        builder.HasKey(cp => cp.Id);

        builder.HasOne<Coupon>()
            .WithMany()
            .HasForeignKey(cp => cp.CouponId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(cp => cp.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(cp => new { cp.CouponId, cp.ProductId }).IsUnique();
    }
}