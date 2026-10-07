using Eseller.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations;

[DbContext(typeof(EsellerDbContext))]
[Migration("20261007080000_AddPaymentShowcaseLogos")]
public partial class AddPaymentShowcaseLogos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[PaymentShowcaseLogos]', N'U') IS NULL
BEGIN
    CREATE TABLE [PaymentShowcaseLogos] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [ImageUrl] nvarchar(500) NOT NULL,
        [SortOrder] int NOT NULL CONSTRAINT [DF_PaymentShowcaseLogos_SortOrder] DEFAULT 0,
        [IsActive] bit NOT NULL CONSTRAINT [DF_PaymentShowcaseLogos_IsActive] DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentShowcaseLogos] PRIMARY KEY ([Id])
    );
    CREATE NONCLUSTERED INDEX [IX_PaymentShowcaseLogos_IsActive] ON [PaymentShowcaseLogos]([IsActive]);
    CREATE NONCLUSTERED INDEX [IX_PaymentShowcaseLogos_SortOrder] ON [PaymentShowcaseLogos]([SortOrder]);
END
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[PaymentShowcaseLogos]', N'U') IS NOT NULL
    DROP TABLE [PaymentShowcaseLogos];
");
    }
}
