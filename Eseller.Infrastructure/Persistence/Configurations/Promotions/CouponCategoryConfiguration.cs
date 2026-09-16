using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Promotions;

public class CouponCategoryConfiguration : IEntityTypeConfiguration<CouponCategory>
{
    public void Configure(EntityTypeBuilder<CouponCategory> builder)
    {
        builder.ToTable("CouponCategories");
        builder.HasKey(cc => cc.Id);

        builder.HasOne<Coupon>()
            .WithMany()
            .HasForeignKey(cc => cc.CouponId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(cc => cc.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(cc => new { cc.CouponId, cc.CategoryId }).IsUnique();
    }
}