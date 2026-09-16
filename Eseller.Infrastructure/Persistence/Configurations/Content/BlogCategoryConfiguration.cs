using Eseller.Domain.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Content;

public class BlogCategoryConfiguration : IEntityTypeConfiguration<BlogCategory>
{
    public void Configure(EntityTypeBuilder<BlogCategory> builder)
    {
        builder.ToTable("BlogCategories");
        builder.HasKey(bc => bc.Id);

        builder.Property(bc => bc.Name).IsRequired().HasMaxLength(150);
        builder.Property(bc => bc.Slug).IsRequired().HasMaxLength(200);

        builder.HasIndex(bc => bc.Slug).IsUnique();
    }
}