using Eseller.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(EsellerDbContext))]
[Migration("20261005160000_AddProductIsStorefrontLive")]
public partial class AddProductIsStorefrontLive : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH('Products', 'IsStorefrontLive') IS NULL
BEGIN
    ALTER TABLE [Products] ADD [IsStorefrontLive] bit NOT NULL
        CONSTRAINT DF_Products_IsStorefrontLive DEFAULT 0;
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Products_IsStorefrontLive' AND object_id = OBJECT_ID(N'[Products]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Products_IsStorefrontLive] ON [Products]([IsStorefrontLive]);
END
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH('Products', 'IsStorefrontLive') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_IsStorefrontLive' AND object_id = OBJECT_ID(N'[Products]'))
        DROP INDEX [IX_Products_IsStorefrontLive] ON [Products];
    DECLARE @df sysname;
    SELECT @df = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'[Products]') AND c.name = N'IsStorefrontLive';
    IF @df IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @df + N']');
    ALTER TABLE [Products] DROP COLUMN [IsStorefrontLive];
END
");
    }
}
