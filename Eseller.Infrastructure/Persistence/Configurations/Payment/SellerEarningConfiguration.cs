using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Payment;

public class SellerEarningConfiguration : IEntityTypeConfiguration<SellerEarning>
{
    public void Configure(EntityTypeBuilder<SellerEarning> builder)
    {
        builder.ToTable("SellerEarnings");
        builder.HasKey(se => se.Id);

        builder.Property(se => se.GrossAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(se => se.CommissionAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(se => se.NetAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(se => se.Status).HasConversion<int>().IsRequired();

        builder.HasOne<SellerOrder>()
            .WithMany()
            .HasForeignKey(se => se.SellerOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CommissionRule>()
            .WithMany()
            .HasForeignKey(se => se.CommissionRuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(se => se.SellerOrderId).IsUnique();
        builder.HasIndex(se => se.Status);
    }
}