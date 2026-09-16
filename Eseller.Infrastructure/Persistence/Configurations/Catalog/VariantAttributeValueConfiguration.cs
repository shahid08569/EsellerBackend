using Eseller.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Catalog;

public class VariantAttributeValueConfiguration : IEntityTypeConfiguration<VariantAttributeValue>
{
    public void Configure(EntityTypeBuilder<VariantAttributeValue> builder)
    {
        builder.ToTable("VariantAttributeValues");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.AttributeName).IsRequired().HasMaxLength(100);
        builder.Property(v => v.AttributeValue).IsRequired().HasMaxLength(200);

        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(v => v.ProductVariantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => v.ProductVariantId);
        builder.HasIndex(v => new { v.ProductVariantId, v.AttributeName }).IsUnique();
    }
}