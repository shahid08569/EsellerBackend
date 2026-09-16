using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Orders;

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistories");
        builder.HasKey(osh => osh.Id);

        builder.Property(osh => osh.OldStatus).HasConversion<int>().IsRequired();
        builder.Property(osh => osh.NewStatus).HasConversion<int>().IsRequired();
        builder.Property(osh => osh.ChangedByType).IsRequired().HasMaxLength(50);
        builder.Property(osh => osh.ChangedAt).IsRequired();

        builder.HasOne<OrderRequest>()
            .WithMany()
            .HasForeignKey(osh => osh.OrderRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(osh => osh.ChangedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(osh => osh.OrderRequestId);
        builder.HasIndex(osh => osh.ChangedAt);
    }
}