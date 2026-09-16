using Eseller.Domain.Entities.Identity;
using AffiliateEntity = Eseller.Domain.Entities.Identity.Affiliate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Identity;

public class AffiliateConfiguration : IEntityTypeConfiguration<AffiliateEntity>
{
    public void Configure(EntityTypeBuilder<AffiliateEntity> builder)
    {
        builder.ToTable("Affiliates");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ReferralCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .IsRequired();

        builder.HasOne<Account>()
            .WithOne()
            .HasForeignKey<AffiliateEntity>(a => a.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.AccountId)
            .IsUnique();

        builder.HasIndex(a => a.ReferralCode)
            .IsUnique();
    }
}