using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Wallet;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Type).HasConversion<int>().IsRequired();
        builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Message).IsRequired().HasMaxLength(2000);
        builder.Property(n => n.IsRead).IsRequired().HasDefaultValue(false);
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.UpdatedAt).IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(n => n.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => n.AccountId);
        builder.HasIndex(n => new { n.AccountId, n.IsRead });
        builder.HasIndex(n => n.CreatedAt);
    }
}