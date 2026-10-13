using Eseller.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Catalog;

public class ShopTrafficConfiguration : IEntityTypeConfiguration<ShopTraffic>
{
    public void Configure(EntityTypeBuilder<ShopTraffic> builder)
    {
        builder.ToTable("ShopTraffics");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Date).IsRequired();
        builder.Property(t => t.VisitCount).IsRequired().HasDefaultValue(0);
        builder.Property(t => t.UniqueVisitors).IsRequired().HasDefaultValue(0);
        builder.Property(t => t.IsManual).IsRequired().HasDefaultValue(false);
        builder.Property(t => t.Note).HasMaxLength(500);

        builder.HasOne(t => t.Shop)
            .WithMany()
            .HasForeignKey(t => t.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.ShopId, t.Date }).IsUnique();
        builder.HasIndex(t => t.Date);
    }
}
