using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Orders;

public class SellerOrderConfiguration : IEntityTypeConfiguration<SellerOrder>
{
    public void Configure(EntityTypeBuilder<SellerOrder> builder)
    {
        builder.ToTable("SellerOrders");
        builder.HasKey(so => so.Id);

        builder.Property(so => so.Status).HasConversion<int>().IsRequired();
        builder.Property(so => so.CreatedAt).IsRequired();
        builder.Property(so => so.UpdatedAt).IsRequired();

        builder.HasOne<OrderRequest>()
            .WithMany()
            .HasForeignKey(so => so.OrderRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(so => so.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(so => so.OrderRequestId);
        builder.HasIndex(so => so.ShopId);
        builder.HasIndex(so => so.Status);
    }
}