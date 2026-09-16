using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Entities.Shipping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Shipping;

public class ReturnItemConfiguration : IEntityTypeConfiguration<ReturnItem>
{
    public void Configure(EntityTypeBuilder<ReturnItem> builder)
    {
        builder.ToTable("ReturnItems");
        builder.HasKey(ri => ri.Id);

        builder.Property(ri => ri.Quantity).IsRequired();

        builder.HasOne<ReturnRequest>()
            .WithMany()
            .HasForeignKey(ri => ri.ReturnRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<OrderRequestItem>()
            .WithMany()
            .HasForeignKey(ri => ri.OrderRequestItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ri => ri.ReturnRequestId);
        builder.HasIndex(ri => ri.OrderRequestItemId);
    }
}