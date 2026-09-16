using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Orders;

public class OrderRequestConfiguration : IEntityTypeConfiguration<OrderRequest>
{
    public void Configure(EntityTypeBuilder<OrderRequest> builder)
    {
        builder.ToTable("OrderRequests");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Status).HasConversion<int>().IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.UserId);
        builder.HasIndex(o => o.Status);
    }
}