using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Identity;

public class ShopkeeperConfiguration : IEntityTypeConfiguration<Shopkeeper>
{
    public void Configure(EntityTypeBuilder<Shopkeeper> builder)
    {
        builder.ToTable("Shopkeepers");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(s => s.Phone)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(s => s.DocumentType).IsRequired(false);
        builder.Property(s => s.DocumentNumber).IsRequired(false);
        builder.Property(s => s.CnicFrontUrl).IsRequired(false);
        builder.Property(s => s.CnicBackUrl).IsRequired(false);
        builder.Property(s => s.DocumentUrl).IsRequired(false);

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.ApprovedByAccountId)
            .IsRequired(false);

        builder.Property(s => s.ApprovedAt)
            .IsRequired(false);

        builder.Property(s => s.RejectionReason)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(s => s.PartnerId)
            .IsRequired(false);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        // 1:1 with Account
        builder.HasOne<Account>()
            .WithOne()
            .HasForeignKey<Shopkeeper>(s => s.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // ApprovedBy -> Account
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(s => s.ApprovedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.AccountId)
            .IsUnique();
    }
}