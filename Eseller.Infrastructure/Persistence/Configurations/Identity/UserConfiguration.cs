using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Identity;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(u => u.Phone)
            .HasMaxLength(30)
            .IsRequired(false);

        builder.Property(u => u.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(Eseller.Domain.Enums.UserApprovalStatus.Approved);

        builder.Property(u => u.RejectionReason)
            .HasMaxLength(500);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .IsRequired();

        // 1:1 with Account
        builder.HasOne<Account>()
            .WithOne()
            .HasForeignKey<User>(u => u.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.AccountId)
            .IsUnique();

        builder.HasIndex(u => u.Status);
    }
}
