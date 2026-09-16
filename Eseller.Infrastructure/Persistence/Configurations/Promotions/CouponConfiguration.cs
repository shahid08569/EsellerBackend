using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Promotions;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("Coupons");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Type).HasConversion<int>().IsRequired();
        builder.Property(c => c.Value).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.MinOrderAmount).HasPrecision(18, 2);
        builder.Property(c => c.MaxDiscount).HasPrecision(18, 2);
        builder.Property(c => c.UsedCount).IsRequired().HasDefaultValue(0);
        builder.Property(c => c.ScopeType).HasConversion<int>().IsRequired();
        builder.Property(c => c.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(c => c.CreatedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasIndex(c => c.IsActive);
    }
}