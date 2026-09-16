using Eseller.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Payment;

public class CommissionRuleConfiguration : IEntityTypeConfiguration<CommissionRule>
{
    public void Configure(EntityTypeBuilder<CommissionRule> builder)
    {
        builder.ToTable("CommissionRules");
        builder.HasKey(cr => cr.Id);

        builder.Property(cr => cr.ScopeType).HasConversion<int>().IsRequired();
        builder.Property(cr => cr.Type).HasConversion<int>().IsRequired();
        builder.Property(cr => cr.Value).HasPrecision(18, 2).IsRequired();
        builder.Property(cr => cr.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(cr => cr.CreatedAt).IsRequired();
        builder.Property(cr => cr.UpdatedAt).IsRequired();

        builder.HasIndex(cr => new { cr.ScopeType, cr.ScopeId });
        builder.HasIndex(cr => cr.IsActive);
    }
}