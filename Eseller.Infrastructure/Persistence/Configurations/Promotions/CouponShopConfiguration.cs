using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Promotions;

public class CouponShopConfiguration : IEntityTypeConfiguration<CouponShop>
{
    public void Configure(EntityTypeBuilder<CouponShop> builder)
    {
        builder.ToTable("CouponShops");
        builder.HasKey(cs => cs.Id);

        builder.HasOne<Coupon>()
            .WithMany()
            .HasForeignKey(cs => cs.CouponId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(cs => cs.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(cs => new { cs.CouponId, cs.ShopId }).IsUnique();
    }
}