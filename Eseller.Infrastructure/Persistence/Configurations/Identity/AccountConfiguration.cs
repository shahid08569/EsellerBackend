using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Identity;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Username)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Email)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(a => a.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.RoleType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(a => a.IsEmailVerified)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.IsPhoneVerified)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.LastLoginAt)
            .IsRequired(false);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .IsRequired();

        builder.HasIndex(a => a.Username)
            .IsUnique();

        builder.HasIndex(a => a.Email)
            .IsUnique();
    }
}