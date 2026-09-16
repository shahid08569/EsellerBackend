using Eseller.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Payment;

public class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable("PaymentAttempts");
        builder.HasKey(pa => pa.Id);

        builder.Property(pa => pa.AttemptNumber).IsRequired();
        builder.Property(pa => pa.Status).HasConversion<int>().IsRequired();
        builder.Property(pa => pa.ProviderResponse).HasColumnType("nvarchar(max)");
        builder.Property(pa => pa.CreatedAt).IsRequired();

        builder.HasOne<Eseller.Domain.Entities.Payment.Payment>()
            .WithMany()
            .HasForeignKey(pa => pa.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pa => pa.PaymentId);
    }
}