using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Identity;

public class SuperAdminConfiguration : IEntityTypeConfiguration<SuperAdmin>
{
    public void Configure(EntityTypeBuilder<SuperAdmin> builder)
    {
        builder.ToTable("SuperAdmins");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        builder.HasOne<Account>()
            .WithOne()
            .HasForeignKey<SuperAdmin>(s => s.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.AccountId)
            .IsUnique();
    }
}