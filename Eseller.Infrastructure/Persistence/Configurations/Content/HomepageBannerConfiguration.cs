using Eseller.Domain.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Content;

public class HomepageBannerConfiguration : IEntityTypeConfiguration<HomepageBanner>
{
    public void Configure(EntityTypeBuilder<HomepageBanner> builder)
    {
        builder.ToTable("HomepageBanners");
        builder.HasKey(hb => hb.Id);

        builder.Property(hb => hb.ImageUrl).IsRequired().HasMaxLength(500);
        builder.Property(hb => hb.LinkUrl).HasMaxLength(500);
        builder.Property(hb => hb.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(hb => hb.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(hb => hb.CreatedAt).IsRequired();
        builder.Property(hb => hb.UpdatedAt).IsRequired();

        builder.HasIndex(hb => hb.IsActive);
        builder.HasIndex(hb => hb.SortOrder);
    }
}