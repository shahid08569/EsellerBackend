using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Engagement;
using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Engagement;

public class ProductQuestionConfiguration : IEntityTypeConfiguration<ProductQuestion>
{
    public void Configure(EntityTypeBuilder<ProductQuestion> builder)
    {
        builder.ToTable("ProductQuestions");
        builder.HasKey(pq => pq.Id);

        builder.Property(pq => pq.Question).IsRequired().HasMaxLength(2000);
        builder.Property(pq => pq.Answer).HasMaxLength(4000);
        builder.Property(q => q.IsApproved).IsRequired().HasDefaultValue(false);
        builder.Property(pq => pq.CreatedAt).IsRequired();

        builder.Property(q => q.IsDeleted).IsRequired().HasDefaultValue(false);


        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(pq => pq.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(pq => pq.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Eseller.Domain.Entities.Identity.Shopkeeper>()
            .WithMany()
            .HasForeignKey(pq => pq.AnsweredByShopkeeperId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(pq => pq.ProductId);
        builder.HasIndex(pq => new { pq.ProductId, pq.IsApproved });
        builder.HasIndex(pq => pq.UserId);

        builder.HasQueryFilter(q => !q.IsDeleted);
    }
}