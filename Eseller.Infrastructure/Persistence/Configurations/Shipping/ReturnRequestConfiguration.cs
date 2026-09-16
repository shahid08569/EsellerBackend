using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Entities.Shipping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Shipping;

public class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> builder)
    {
        builder.ToTable("ReturnRequests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Reason).IsRequired().HasMaxLength(500);
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.Status).HasConversion<int>().IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasOne<SellerOrder>()
            .WithMany()
            .HasForeignKey(r => r.SellerOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(r => r.ProcessedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.SellerOrderId);
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.Status);
    }
}