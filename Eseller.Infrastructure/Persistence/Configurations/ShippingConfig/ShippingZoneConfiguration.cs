using Eseller.Domain.Entities.ShippingConfig;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.ShippingConfig;

public class ShippingZoneConfiguration : IEntityTypeConfiguration<ShippingZone>
{
    public void Configure(EntityTypeBuilder<ShippingZone> builder)
    {
        builder.ToTable("ShippingZones");
        builder.HasKey(sz => sz.Id);

        builder.Property(sz => sz.Name).IsRequired().HasMaxLength(150);
        builder.Property(sz => sz.Regions).HasColumnType("nvarchar(max)");
        builder.Property(sz => sz.CreatedAt).IsRequired();
        builder.Property(sz => sz.UpdatedAt).IsRequired();
    }
}