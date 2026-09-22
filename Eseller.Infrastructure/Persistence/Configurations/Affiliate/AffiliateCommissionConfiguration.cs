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

        // Properties
        builder.Property(ac => ac.CommissionAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(ac => ac.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(ac => ac.CreatedAt)
            .IsRequired();

        builder.Property(ac => ac.ApprovedAt)
            .IsRequired(false);

        builder.Property(ac => ac.PaidAt)
            .IsRequired(false);

        // FKs
        builder.HasOne<Eseller.Domain.Entities.Identity.Affiliate>()
            .WithMany()
            .HasForeignKey(ac => ac.AffiliateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<OrderRequest>()
            .WithMany()
            .HasForeignKey(ac => ac.OrderRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<OrderRequestItem>()
            .WithMany()
            .HasForeignKey(ac => ac.OrderRequestItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AffiliateClick>()
            .WithMany()
            .HasForeignKey(ac => ac.AffiliateClickId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(ac => ac.AffiliateId);
        builder.HasIndex(ac => ac.OrderRequestId);
        builder.HasIndex(ac => ac.OrderRequestItemId);
        builder.HasIndex(ac => ac.Status);
        builder.HasIndex(ac => new { ac.AffiliateId, ac.Status });
    }
}