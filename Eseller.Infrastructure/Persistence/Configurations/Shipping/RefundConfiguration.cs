using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Shipping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Shipping;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(r => r.Status).HasConversion<int>().IsRequired();
        builder.Property(r => r.RefundMethod).HasMaxLength(100);
        builder.Property(r => r.ProviderReference).HasMaxLength(200);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasOne<Eseller.Domain.Entities.Payment.Payment>()
            .WithMany()
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ReturnRequest>()
            .WithMany()
            .HasForeignKey(r => r.ReturnRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(r => r.ProcessedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.PaymentId);
        builder.HasIndex(r => r.ReturnRequestId);
        builder.HasIndex(r => r.Status);
    }
}