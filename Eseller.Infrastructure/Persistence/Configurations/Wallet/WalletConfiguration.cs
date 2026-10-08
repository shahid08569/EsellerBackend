using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Wallet;

public class WalletConfiguration : IEntityTypeConfiguration<Eseller.Domain.Entities.Wallet.Wallet>
{
    public void Configure(EntityTypeBuilder<Eseller.Domain.Entities.Wallet.Wallet> builder)
    {
        builder.ToTable("Wallets");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Balance).HasPrecision(18, 2).IsRequired().HasDefaultValue(0);
        builder.Property(w => w.PendingEarnings).HasPrecision(18, 2).IsRequired().HasDefaultValue(0);
        builder.Property(w => w.AvailableEarnings).HasPrecision(18, 2).IsRequired().HasDefaultValue(0);
        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.UpdatedAt).IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(w => w.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => w.AccountId).IsUnique();
    }
}