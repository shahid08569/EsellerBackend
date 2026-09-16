using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Identity;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TokenHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(r => r.FamilyId)
            .IsRequired();

        builder.Property(r => r.ReplacedByTokenId)
            .IsRequired(false);

        builder.Property(r => r.ExpiresAt)
            .IsRequired();

        builder.Property(r => r.RevokedAt)
            .IsRequired(false);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(r => r.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(r => r.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.TokenHash)
            .IsUnique();

        builder.HasIndex(r => r.AccountId);

        builder.HasIndex(r => r.FamilyId);
    }
}