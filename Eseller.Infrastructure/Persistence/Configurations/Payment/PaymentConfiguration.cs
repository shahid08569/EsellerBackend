using Eseller.Domain.Entities.Orders;
using PaymentEntity = Eseller.Domain.Entities.Payment.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Payment;

public class PaymentConfiguration : IEntityTypeConfiguration<PaymentEntity>
{
    public void Configure(EntityTypeBuilder<PaymentEntity> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Method).HasConversion<int>().IsRequired();
        builder.Property(p => p.Provider).HasMaxLength(100);
        builder.Property(p => p.ProviderTransactionId).HasMaxLength(200);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("PKR");
        builder.Property(p => p.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.Status).HasConversion<int>().IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        builder.HasOne<OrderRequest>()
            .WithMany()
            .HasForeignKey(p => p.OrderRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.OrderRequestId);
        builder.HasIndex(p => new { p.Provider, p.ProviderTransactionId })
            .IsUnique()
            .HasFilter("[ProviderTransactionId] IS NOT NULL");
        builder.HasIndex(p => p.Status);
    }
}