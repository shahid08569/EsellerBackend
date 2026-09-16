using Eseller.Domain.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Content;

public class CmsPageConfiguration : IEntityTypeConfiguration<CmsPage>
{
    public void Configure(EntityTypeBuilder<CmsPage> builder)
    {
        builder.ToTable("CmsPages");
        builder.HasKey(cp => cp.Id);

        builder.Property(cp => cp.Slug).IsRequired().HasMaxLength(200);
        builder.Property(cp => cp.Title).IsRequired().HasMaxLength(300);
        builder.Property(cp => cp.Content).IsRequired();
        builder.Property(cp => cp.CreatedAt).IsRequired();
        builder.Property(cp => cp.UpdatedAt).IsRequired();

        builder.HasIndex(cp => cp.Slug).IsUnique();
    }
}