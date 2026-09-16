using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.ShippingConfig;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.ShippingConfig;

public class ShippingMethodConfiguration : IEntityTypeConfiguration<ShippingMethod>
{
    public void Configure(EntityTypeBuilder<ShippingMethod> builder)
    {
        builder.ToTable("ShippingMethods");
        builder.HasKey(sm => sm.Id);

        builder.Property(sm => sm.Name).IsRequired().HasMaxLength(150);
        builder.Property(sm => sm.Rate).HasPrecision(18, 2).IsRequired();
        builder.Property(sm => sm.IsFree).IsRequired().HasDefaultValue(false);
        builder.Property(sm => sm.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(sm => sm.CreatedAt).IsRequired();
        builder.Property(sm => sm.UpdatedAt).IsRequired();

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(sm => sm.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ShippingZone>()
            .WithMany()
            .HasForeignKey(sm => sm.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(sm => sm.ShopId);
        builder.HasIndex(sm => sm.ZoneId);
        builder.HasIndex(sm => sm.IsActive);
    }
}