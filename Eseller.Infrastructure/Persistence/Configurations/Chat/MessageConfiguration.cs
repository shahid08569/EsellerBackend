using Eseller.Domain.Entities.Chat;
using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Chat;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.SenderRole).HasConversion<int>().IsRequired();
        builder.Property(m => m.Content).IsRequired().HasMaxLength(4000);
        builder.Property(m => m.SentAt).IsRequired();
        builder.Property(m => m.IsRead).IsRequired().HasDefaultValue(false);
        builder.Property(m => m.DeliveredAt).IsRequired(false);
        builder.Property(m => m.AttachmentUrl).HasMaxLength(1000);
        builder.Property(m => m.AttachmentFileName).HasMaxLength(260);
        builder.Property(m => m.AttachmentContentType).HasMaxLength(200);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.ConversationId);
        builder.HasIndex(m => m.SentAt);
        builder.HasIndex(m => new { m.ConversationId, m.IsRead });
    }
}
