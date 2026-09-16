using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Orders;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.HasKey(cm => cm.Id);

        builder.Property(cm => cm.SenderRole).HasConversion<int>().IsRequired();
        builder.Property(cm => cm.Message).IsRequired().HasMaxLength(4000);
        builder.Property(cm => cm.SentAt).IsRequired();
        builder.Property(cm => cm.IsRead).IsRequired().HasDefaultValue(false);

        builder.HasOne<OrderRequest>()
            .WithMany()
            .HasForeignKey(cm => cm.OrderRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(cm => cm.SenderAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(cm => cm.OrderRequestId);
        builder.HasIndex(cm => cm.SentAt);
        builder.HasIndex(cm => new { cm.OrderRequestId, cm.IsRead });
    }
}