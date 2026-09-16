using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Entities.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Promotions;

public class CouponUsageConfiguration : IEntityTypeConfiguration<CouponUsage>
{
    public void Configure(EntityTypeBuilder<CouponUsage> builder)
    {
        builder.ToTable("CouponUsages");
        builder.HasKey(cu => cu.Id);

        builder.Property(cu => cu.UsedAt).IsRequired();

        builder.HasOne<Coupon>()
            .WithMany()
            .HasForeignKey(cu => cu.CouponId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<OrderRequest>()
            .WithMany()
            .HasForeignKey(cu => cu.OrderRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(cu => cu.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(cu => cu.OrderRequestId).IsUnique();
        builder.HasIndex(cu => cu.CouponId);
        builder.HasIndex(cu => cu.UserId);
    }
}