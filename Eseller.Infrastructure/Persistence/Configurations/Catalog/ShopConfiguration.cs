using Eseller.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Catalog;

public class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("Shops");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Slug).IsRequired().HasMaxLength(250);
        builder.Property(s => s.Description).HasMaxLength(2000);
        builder.Property(s => s.LogoUrl).HasMaxLength(500);
        builder.Property(s => s.BannerUrl).HasMaxLength(500);
        builder.Property(s => s.Phone).HasMaxLength(30);
        builder.Property(s => s.Address).HasMaxLength(500);
        builder.Property(s => s.City).HasMaxLength(100);
        builder.Property(s => s.Country).HasMaxLength(100);
        builder.Property(s => s.Rating).HasPrecision(3, 2);
        builder.Property(s => s.TotalProducts).IsRequired().HasDefaultValue(0);
        builder.Property(s => s.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasOne<Eseller.Domain.Entities.Identity.Shopkeeper>()
            .WithMany()
            .HasForeignKey(s => s.ShopkeeperId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.Slug).IsUnique();
        builder.HasIndex(s => s.ShopkeeperId);
        builder.HasIndex(s => s.IsActive);

        builder.HasQueryFilter(s => s.DeletedAt == null);
    }
}