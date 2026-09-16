using Eseller.Domain.Entities.Affiliate;
using Eseller.Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Affiliate;

public class AffiliateCommissionConfiguration : IEntityTypeConfiguration<AffiliateCommission>
{
    public void Configure(EntityTypeBuilder<AffiliateCommission> builder)
    {
        builder.ToTable("AffiliateCommissions");
        builder.HasKey(ac => ac.Id);

        builder.Property(ac => ac.CommissionAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(ac => ac.Status).HasConversion<int>().IsRequired();
        builder.Property(ac => ac.CreatedAt).IsRequired();

        builder.HasOne<Eseller.Domain.Entities.Identity.Affiliate>()
            .WithMany()
            .HasForeignKey(ac => ac.AffiliateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<OrderRequestItem>()
            .WithMany()
            .HasForeignKey(ac => ac.OrderRequestItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AffiliateClick>()
            .WithMany()
            .HasForeignKey(ac => ac.AffiliateClickId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ac => ac.AffiliateId);
        builder.HasIndex(ac => ac.OrderRequestItemId);
        builder.HasIndex(ac => ac.Status);
    }
}