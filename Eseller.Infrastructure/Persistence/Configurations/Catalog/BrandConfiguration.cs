using Eseller.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Catalog;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name).IsRequired().HasMaxLength(150);
        builder.Property(b => b.Slug).IsRequired().HasMaxLength(200);
        builder.Property(b => b.LogoUrl).HasMaxLength(500);
        builder.Property(b => b.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.UpdatedAt).IsRequired();

        builder.HasIndex(b => b.Slug).IsUnique();
        builder.HasIndex(b => b.IsActive);

        // ✅ Soft delete query filter
        builder.HasQueryFilter(b => b.IsActive);
    }
}