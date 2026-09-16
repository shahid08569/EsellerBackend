using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Entities.Shipping;
using Eseller.Domain.Entities.ShippingConfig;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Shipping;

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Carrier).HasMaxLength(100);
        builder.Property(s => s.TrackingNumber).HasMaxLength(100);
        builder.Property(s => s.Status).HasConversion<int>().IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasOne<SellerOrder>()
            .WithMany()
            .HasForeignKey(s => s.SellerOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ShippingMethod>()
            .WithMany()
            .HasForeignKey(s => s.ShippingMethodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.SellerOrderId).IsUnique();
        builder.HasIndex(s => s.TrackingNumber);
        builder.HasIndex(s => s.Status);
    }
}