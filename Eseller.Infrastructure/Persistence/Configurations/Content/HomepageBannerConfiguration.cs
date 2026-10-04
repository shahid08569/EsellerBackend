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
        builder.Property(hb => hb.Title).HasMaxLength(200);
        builder.Property(hb => hb.Subtitle).HasMaxLength(200);
        builder.Property(hb => hb.Description).HasMaxLength(1000);
        builder.Property(hb => hb.Price).HasPrecision(18, 2);
        builder.Property(hb => hb.OriginalPrice).HasPrecision(18, 2);
        builder.Property(hb => hb.ButtonText).HasMaxLength(100);
        builder.Property(hb => hb.BackgroundColor).HasMaxLength(300);
        builder.Property(hb => hb.TextColor).HasMaxLength(50);
        builder.Property(hb => hb.AvailableSizes).HasMaxLength(100);
        builder.Property(hb => hb.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(hb => hb.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(hb => hb.CreatedAt).IsRequired();
        builder.Property(hb => hb.UpdatedAt).IsRequired();

        builder.HasIndex(hb => hb.IsActive);
        builder.HasIndex(hb => hb.SortOrder);
    }
}