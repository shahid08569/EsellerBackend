using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Catalog;

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("InventoryTransactions");
        builder.HasKey(it => it.Id);

        builder.Property(it => it.TransactionType).HasConversion<int>().IsRequired();
        builder.Property(it => it.Quantity).IsRequired();
        builder.Property(it => it.PreviousQuantity).IsRequired();
        builder.Property(it => it.NewQuantity).IsRequired();
        builder.Property(it => it.ReferenceType).IsRequired().HasMaxLength(100);
        builder.Property(it => it.CreatedAt).IsRequired();

        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(it => it.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(it => it.CreatedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(it => it.ProductVariantId);
        builder.HasIndex(it => new { it.ReferenceType, it.ReferenceId });
        builder.HasIndex(it => it.CreatedAt);
    }
}