using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WidenHomepageBannerBackgroundColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('HomepageBanners', 'BackgroundColor') IS NOT NULL
                    ALTER TABLE [HomepageBanners] ALTER COLUMN [BackgroundColor] nvarchar(300) NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('HomepageBanners', 'BackgroundColor') IS NOT NULL
                    ALTER TABLE [HomepageBanners] ALTER COLUMN [BackgroundColor] nvarchar(50) NULL;
                """);
        }
    }
}
