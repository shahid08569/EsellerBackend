using Eseller.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eseller.Infrastructure.Persistence.Configurations.Identity;

public class LocationLogConfiguration : IEntityTypeConfiguration<LocationLog>
{
    public void Configure(EntityTypeBuilder<LocationLog> builder)
    {
        builder.ToTable("LocationLogs");

        builder.HasKey(ll => ll.Id);

        builder.Property(ll => ll.Latitude)
            .HasPrecision(10, 7)
            .IsRequired();

        builder.Property(ll => ll.Longitude)
            .HasPrecision(10, 7)
            .IsRequired();

        builder.Property(ll => ll.City)
    .HasMaxLength(100);

        builder.Property(ll => ll.Country)
            .HasMaxLength(100);

        builder.Property(ll => ll.CountryCode)
            .HasMaxLength(5);

        builder.Property(ll => ll.DeviceType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(ll => ll.IpAddress)
            .HasMaxLength(45);

        builder.Property(ll => ll.UserAgent)
            .HasMaxLength(500);

        builder.Property(ll => ll.Action)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ll => ll.RecordedAt)
            .IsRequired();

        // FK to Account (nullable — guest users ke liye bhi log ho sakta)
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(ll => ll.AccountId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(ll => ll.AccountId);
        builder.HasIndex(ll => ll.RecordedAt);
        builder.HasIndex(ll => ll.IpAddress);
        builder.HasIndex(ll => ll.CountryCode);
    }
}