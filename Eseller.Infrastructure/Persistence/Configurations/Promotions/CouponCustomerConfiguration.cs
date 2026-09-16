using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Promotions;

public class CouponCustomerConfiguration : IEntityTypeConfiguration<CouponCustomer>
{
    public void Configure(EntityTypeBuilder<CouponCustomer> builder)
    {
        builder.ToTable("CouponCustomers");
        builder.HasKey(cc => cc.Id);

        builder.HasOne<Coupon>()
            .WithMany()
            .HasForeignKey(cc => cc.CouponId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(cc => cc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(cc => new { cc.CouponId, cc.UserId }).IsUnique();
    }
}