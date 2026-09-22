using Eseller.Domain.Entities.Affiliate;
using Eseller.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Affiliate;

public class AffiliateClickConfiguration : IEntityTypeConfiguration<AffiliateClick>
{
    public void Configure(EntityTypeBuilder<AffiliateClick> builder)
    {
        builder.ToTable("AffiliateClicks");
        builder.HasKey(ac => ac.Id);

        // Properties
        builder.Property(ac => ac.ClickedAt)
            .IsRequired();

        builder.Property(ac => ac.IpAddress)
            .HasMaxLength(45);

        builder.Property(ac => ac.UserAgent)
            .HasMaxLength(500);

        builder.Property(ac => ac.Converted)
            .IsRequired()
            .HasDefaultValue(false);

        // FKs
        builder.HasOne<Eseller.Domain.Entities.Identity.Affiliate>()
            .WithMany()
            .HasForeignKey(ac => ac.AffiliateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(ac => ac.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(ac => ac.AffiliateId);
        builder.HasIndex(ac => ac.ProductId);
        builder.HasIndex(ac => ac.ClickedAt);
        builder.HasIndex(ac => new { ac.AffiliateId, ac.Converted });
    }
}