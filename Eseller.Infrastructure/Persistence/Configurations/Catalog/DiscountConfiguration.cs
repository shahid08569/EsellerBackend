using Eseller.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Catalog;

public class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> builder)
    {
        builder.ToTable("Discounts");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.ScopeType).HasConversion<int>().IsRequired();
        builder.Property(d => d.Type).HasConversion<int>().IsRequired();
        builder.Property(d => d.Value).HasPrecision(18, 2).IsRequired();
        builder.Property(d => d.StartDate).IsRequired();
        builder.Property(d => d.EndDate).IsRequired();
        builder.Property(d => d.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.UpdatedAt).IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.ProductId);
        builder.HasIndex(d => new { d.ScopeType, d.ScopeId });
        builder.HasIndex(d => d.IsActive);
    }
}