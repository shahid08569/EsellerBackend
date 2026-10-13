using Eseller.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations;

[DbContext(typeof(EsellerDbContext))]
[Migration("20261013120000_AddShopTrafficTable")]
public partial class AddShopTrafficTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'ShopTraffics', N'U') IS NULL
BEGIN
    CREATE TABLE [ShopTraffics] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [ShopId] uniqueidentifier NOT NULL,
        [Date] datetime2 NOT NULL,
        [VisitCount] int NOT NULL CONSTRAINT [DF_ShopTraffics_VisitCount] DEFAULT 0,
        [UniqueVisitors] int NOT NULL CONSTRAINT [DF_ShopTraffics_UniqueVisitors] DEFAULT 0,
        [IsManual] bit NOT NULL CONSTRAINT [DF_ShopTraffics_IsManual] DEFAULT 0,
        [Note] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_ShopTraffics_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [FK_ShopTraffics_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_ShopTraffics_ShopId_Date] ON [ShopTraffics] ([ShopId], [Date]);
    CREATE INDEX [IX_ShopTraffics_Date] ON [ShopTraffics] ([Date]);
END
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'ShopTraffics', N'U') IS NOT NULL
BEGIN
    DROP TABLE [ShopTraffics];
END
");
    }
}
