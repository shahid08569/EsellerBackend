using Eseller.Domain.Entities.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Wallet;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("WalletTransactions");
        builder.HasKey(wt => wt.Id);

        builder.Property(wt => wt.Direction).HasConversion<int>().IsRequired();
        builder.Property(wt => wt.TransactionType).HasConversion<int>().IsRequired();
        builder.Property(wt => wt.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(wt => wt.ReferenceType).HasMaxLength(100);
        builder.Property(wt => wt.Description).HasMaxLength(500);
        builder.Property(wt => wt.CreatedAt).IsRequired();

        builder.HasOne<Eseller.Domain.Entities.Wallet.Wallet>()
            .WithMany()
            .HasForeignKey(wt => wt.WalletId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(wt => wt.WalletId);
        builder.HasIndex(wt => wt.CreatedAt);
        builder.HasIndex(wt => new { wt.ReferenceType, wt.ReferenceId });
    }
}