using Eseller.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Catalog;

public class ShopImageConfiguration : IEntityTypeConfiguration<ShopImage>
{
    public void Configure(EntityTypeBuilder<ShopImage> builder)
    {
        builder.ToTable("ShopImages");
        builder.HasKey(si => si.Id);

        builder.Property(si => si.ImageUrl).IsRequired().HasMaxLength(500);
        builder.Property(si => si.Type).IsRequired().HasMaxLength(50);

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(si => si.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(si => si.ShopId);
    }
}