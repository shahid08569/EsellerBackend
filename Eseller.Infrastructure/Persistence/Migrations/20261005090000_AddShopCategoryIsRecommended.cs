using Eseller.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(EsellerDbContext))]
[Migration("20261005090000_AddShopCategoryIsRecommended")]
public partial class AddShopCategoryIsRecommended : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Separate batches: SQL Server cannot reference a newly added column in the same batch.
        migrationBuilder.Sql(@"
IF COL_LENGTH('ShopCategories', 'IsRecommended') IS NULL
    ALTER TABLE [ShopCategories] ADD [IsRecommended] bit NOT NULL CONSTRAINT DF_ShopCategories_IsRecommended DEFAULT 0;
");
        migrationBuilder.Sql(@"
IF COL_LENGTH('ShopCategories', 'IsRecommended') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM [ShopCategories] WHERE [DeletedAt] IS NULL AND [IsRecommended] = 1)
BEGIN
    UPDATE TOP (1) [ShopCategories]
    SET [IsRecommended] = 1
    WHERE [DeletedAt] IS NULL AND ([Slug] = 'gold' OR [Name] = 'Gold');
END
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH('ShopCategories', 'IsRecommended') IS NOT NULL
BEGIN
    DECLARE @df sysname;
    SELECT @df = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'[ShopCategories]') AND c.name = N'IsRecommended';
    IF @df IS NOT NULL EXEC(N'ALTER TABLE [ShopCategories] DROP CONSTRAINT [' + @df + N']');
    ALTER TABLE [ShopCategories] DROP COLUMN [IsRecommended];
END
");
    }
}
