using Eseller.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds content fields to HomepageBanners that exist on the entity/snapshot
/// but were never applied to the database (migration previously lacked [Migration] attribute).
/// </summary>
[DbContext(typeof(EsellerDbContext))]
[Migration("20261003201500_AddHomepageBannerContentFields")]
public partial class AddHomepageBannerContentFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('HomepageBanners', 'Title') IS NULL
                ALTER TABLE [HomepageBanners] ADD [Title] nvarchar(200) NULL;

            IF COL_LENGTH('HomepageBanners', 'Subtitle') IS NULL
                ALTER TABLE [HomepageBanners] ADD [Subtitle] nvarchar(200) NULL;

            IF COL_LENGTH('HomepageBanners', 'Description') IS NULL
                ALTER TABLE [HomepageBanners] ADD [Description] nvarchar(1000) NULL;

            IF COL_LENGTH('HomepageBanners', 'Price') IS NULL
                ALTER TABLE [HomepageBanners] ADD [Price] decimal(18,2) NULL;

            IF COL_LENGTH('HomepageBanners', 'OriginalPrice') IS NULL
                ALTER TABLE [HomepageBanners] ADD [OriginalPrice] decimal(18,2) NULL;

            IF COL_LENGTH('HomepageBanners', 'ButtonText') IS NULL
                ALTER TABLE [HomepageBanners] ADD [ButtonText] nvarchar(100) NULL;

            IF COL_LENGTH('HomepageBanners', 'BackgroundColor') IS NULL
                ALTER TABLE [HomepageBanners] ADD [BackgroundColor] nvarchar(300) NULL;

            IF COL_LENGTH('HomepageBanners', 'TextColor') IS NULL
                ALTER TABLE [HomepageBanners] ADD [TextColor] nvarchar(50) NULL;

            IF COL_LENGTH('HomepageBanners', 'AvailableSizes') IS NULL
                ALTER TABLE [HomepageBanners] ADD [AvailableSizes] nvarchar(100) NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('HomepageBanners', 'AvailableSizes') IS NOT NULL
                ALTER TABLE [HomepageBanners] DROP COLUMN [AvailableSizes];

            IF COL_LENGTH('HomepageBanners', 'TextColor') IS NOT NULL
                ALTER TABLE [HomepageBanners] DROP COLUMN [TextColor];

            IF COL_LENGTH('HomepageBanners', 'BackgroundColor') IS NOT NULL
                ALTER TABLE [HomepageBanners] DROP COLUMN [BackgroundColor];

            IF COL_LENGTH('HomepageBanners', 'ButtonText') IS NOT NULL
                ALTER TABLE [HomepageBanners] DROP COLUMN [ButtonText];

            IF COL_LENGTH('HomepageBanners', 'OriginalPrice') IS NOT NULL
                ALTER TABLE [HomepageBanners] DROP COLUMN [OriginalPrice];

            IF COL_LENGTH('HomepageBanners', 'Price') IS NOT NULL
                ALTER TABLE [HomepageBanners] DROP COLUMN [Price];

            IF COL_LENGTH('HomepageBanners', 'Description') IS NOT NULL
                ALTER TABLE [HomepageBanners] DROP COLUMN [Description];

            IF COL_LENGTH('HomepageBanners', 'Subtitle') IS NOT NULL
                ALTER TABLE [HomepageBanners] DROP COLUMN [Subtitle];

            IF COL_LENGTH('HomepageBanners', 'Title') IS NOT NULL
                ALTER TABLE [HomepageBanners] DROP COLUMN [Title];
            """);
    }
}
