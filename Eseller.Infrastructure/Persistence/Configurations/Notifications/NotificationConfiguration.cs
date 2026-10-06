using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Notifications;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.RecipientRole).HasConversion<int>().IsRequired();
        builder.Property(n => n.Type).HasConversion<int>().IsRequired();
        builder.Property(n => n.EntityType).HasConversion<int>().IsRequired();
        builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Message).IsRequired().HasMaxLength(2000);
        builder.Property(n => n.NavigationUrl).HasMaxLength(500);
        builder.Property(n => n.EventKey).IsRequired().HasMaxLength(200);
        builder.Property(n => n.IsRead).IsRequired().HasDefaultValue(false);
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.UpdatedAt).IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(n => n.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => n.RecipientUserId);
        builder.HasIndex(n => new { n.RecipientUserId, n.IsRead });
        builder.HasIndex(n => n.CreatedAt);
        builder.HasIndex(n => n.EventKey).IsUnique();
        builder.HasIndex(n => new { n.Type, n.EntityId, n.RecipientUserId });
    }
}
