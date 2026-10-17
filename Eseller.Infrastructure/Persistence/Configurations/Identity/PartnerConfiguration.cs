using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Identity;

public class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.ToTable("Partners");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.AddedAt)
            .IsRequired();

        builder.Property(p => p.Permissions)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(p => p.ReferralCode)
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        builder.HasOne<Account>()
            .WithOne()
            .HasForeignKey<Partner>(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(p => p.AddedBySuperAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.AccountId)
            .IsUnique();
    }
}