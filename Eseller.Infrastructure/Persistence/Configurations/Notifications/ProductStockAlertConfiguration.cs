using Eseller.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Notifications;

public class ProductStockAlertConfiguration : IEntityTypeConfiguration<ProductStockAlert>
{
    public void Configure(EntityTypeBuilder<ProductStockAlert> builder)
    {
        builder.ToTable("ProductStockAlerts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AlertType).IsRequired().HasMaxLength(50);
        builder.Property(a => a.TriggeredAt).IsRequired();

        builder.HasIndex(a => new { a.ProductId, a.AlertType, a.ResolvedAt });
        builder.HasIndex(a => a.ProductId);
    }
}
