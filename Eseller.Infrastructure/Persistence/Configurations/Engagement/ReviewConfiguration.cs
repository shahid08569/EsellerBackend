using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Engagement;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Engagement;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Rating).IsRequired();
        builder.Property(r => r.Title).HasMaxLength(200);
        builder.Property(r => r.Comment).HasMaxLength(4000);
        builder.Property(r => r.IsApproved).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<OrderRequestItem>()
            .WithMany()
            .HasForeignKey(r => r.OrderRequestItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.ProductId);
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => new { r.UserId, r.OrderRequestItemId })
            .IsUnique()
            .HasFilter("[OrderRequestItemId] IS NOT NULL");
    }
}