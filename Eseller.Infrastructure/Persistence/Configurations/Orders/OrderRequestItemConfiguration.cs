using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Orders;

public class OrderRequestItemConfiguration : IEntityTypeConfiguration<OrderRequestItem>
{
    public void Configure(EntityTypeBuilder<OrderRequestItem> builder)
    {
        builder.ToTable("OrderRequestItems");
        builder.HasKey(oi => oi.Id);

        builder.Property(oi => oi.Quantity).IsRequired();
        builder.Property(oi => oi.PriceAtOrder).HasPrecision(18, 2).IsRequired();
        builder.Property(oi => oi.ProductNameSnapshot).IsRequired().HasMaxLength(250);
        builder.Property(oi => oi.VariantAttributesSnapshot).HasColumnType("nvarchar(max)");
        builder.Property(oi => oi.UnitPriceSnapshot).HasPrecision(18, 2).IsRequired();
        builder.Property(oi => oi.TotalPriceSnapshot).HasPrecision(18, 2).IsRequired();
        builder.Property(oi => oi.CreatedAt).IsRequired();

        builder.HasOne<OrderRequest>()
            .WithMany()
            .HasForeignKey(oi => oi.OrderRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<SellerOrder>()
            .WithMany()
            .HasForeignKey(oi => oi.SellerOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(oi => oi.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(oi => oi.OrderRequestId);
        builder.HasIndex(oi => oi.SellerOrderId);
        builder.HasIndex(oi => oi.ProductVariantId);
    }
}