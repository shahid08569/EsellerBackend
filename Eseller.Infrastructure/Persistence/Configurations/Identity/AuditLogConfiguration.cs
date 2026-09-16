using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Identity;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ActorAccountId)
            .IsRequired(false);

        builder.Property(a => a.ActorType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityId)
            .IsRequired();

        builder.Property(a => a.OldValues)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(a => a.NewValues)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45)
            .IsRequired(false);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(a => a.ActorAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.ActorAccountId);

        builder.HasIndex(a => a.CreatedAt);

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}