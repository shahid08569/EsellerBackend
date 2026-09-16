using Eseller.Domain.Entities.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Promotions;

public class FlashSaleConfiguration : IEntityTypeConfiguration<FlashSale>
{
    public void Configure(EntityTypeBuilder<FlashSale> builder)
    {
        builder.ToTable("FlashSales");
        builder.HasKey(fs => fs.Id);

        builder.Property(fs => fs.Name).IsRequired().HasMaxLength(200);
        builder.Property(fs => fs.StartDate).IsRequired();
        builder.Property(fs => fs.EndDate).IsRequired();
        builder.Property(fs => fs.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(fs => fs.CreatedAt).IsRequired();
        builder.Property(fs => fs.UpdatedAt).IsRequired();

        builder.HasIndex(fs => fs.IsActive);
        builder.HasIndex(fs => new { fs.StartDate, fs.EndDate });
    }
}