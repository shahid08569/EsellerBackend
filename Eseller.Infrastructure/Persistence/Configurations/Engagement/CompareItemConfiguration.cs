using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Engagement;
using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Engagement;

public class CompareItemConfiguration : IEntityTypeConfiguration<CompareItem>
{
    public void Configure(EntityTypeBuilder<CompareItem> builder)
    {
        builder.ToTable("CompareItems");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CreatedAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.UserId, c.ProductId }).IsUnique();
    }
}