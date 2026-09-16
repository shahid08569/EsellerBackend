using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Payment;

public class WithdrawalConfiguration : IEntityTypeConfiguration<Withdrawal>
{
    public void Configure(EntityTypeBuilder<Withdrawal> builder)
    {
        builder.ToTable("Withdrawals");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(w => w.PayoutMethod).HasMaxLength(100);
        builder.Property(w => w.Status).HasConversion<int>().IsRequired();
        builder.Property(w => w.RequestedAt).IsRequired();
        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.UpdatedAt).IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(w => w.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => w.AccountId);
        builder.HasIndex(w => w.Status);
        builder.HasIndex(w => w.RequestedAt);
    }
}