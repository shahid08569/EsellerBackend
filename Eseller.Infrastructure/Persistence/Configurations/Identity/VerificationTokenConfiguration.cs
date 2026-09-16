using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Identity;

public class VerificationTokenConfiguration : IEntityTypeConfiguration<VerificationToken>
{
    public void Configure(EntityTypeBuilder<VerificationToken> builder)
    {
        builder.ToTable("VerificationTokens");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Purpose)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(v => v.CodeHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(v => v.Channel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(v => v.ExpiresAt)
            .IsRequired();

        builder.Property(v => v.Attempts)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(v => v.ConsumedAt)
            .IsRequired(false);

        builder.Property(v => v.CreatedAt)
            .IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(v => v.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => v.AccountId);

        builder.HasIndex(v => new { v.AccountId, v.Purpose });
    }
}